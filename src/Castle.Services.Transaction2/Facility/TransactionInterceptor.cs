namespace Castle.Services.Transaction.Facility
{
	using System;
	using System.Diagnostics.Contracts;
	using System.Runtime.CompilerServices;
	using System.Threading.Tasks;
	using System.Transactions;
	using Core;
	using Core.Interceptor;
	using Core.Logging;
	using DynamicProxy;
	using MicroKernel;
	using Transaction;
	using Zipkin;


	class TransactionInterceptor : IInterceptor, IOnBehalfAware
	{
		private readonly IKernel _kernel;
		private readonly ITransactionMetaInfoStore _store;
		private TransactionalClassMetaInfo _meta;
		private ILogger _logger = NullLogger.Instance;
		private ITransactionManager2 _txManager;

		public TransactionInterceptor(IKernel kernel, ITransactionMetaInfoStore store)
		{
			_kernel = kernel;
			_store = store;
			_txManager = _kernel.Resolve<ITransactionManager2>();
		}

		public ILogger Logger
		{
			get { return _logger; }
			set { _logger = value; }
		}

		public void SetInterceptedComponentModel(ComponentModel target)
		{
			_meta = _store.GetMetaFromType(target.Implementation);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsMethodTransactional(IInvocation invocation, out Castle.Services.Transaction.TransactionOptions? opts)
		{
			var keyMethod = invocation.Method.DeclaringType.IsInterface
				? invocation.MethodInvocationTarget
				: invocation.Method;

			opts = _meta.AsTransactional(keyMethod);

			return opts.HasValue;
		}

		public void Intercept(IInvocation invocation)
		{
//			var txManager = _kernel.Resolve<ITransactionManager2>();

			Castle.Services.Transaction.TransactionOptions? opts;
			if (_txManager.HasTransaction || !IsMethodTransactional(invocation, out opts) )
			{
				// nothing to do - no support for nesting transactions for now

				invocation.Proceed();

				return;
			}

			var transaction = _txManager.CreateTransaction(opts.Value);
			if (typeof(Task).IsAssignableFrom(invocation.MethodInvocationTarget.ReturnType))
			{
				AsyncCase(invocation, transaction);
			}
			else
			{
				SynchronizedCase(invocation, transaction);
			}
		}

		private void AsyncCase(IInvocation invocation, ITransaction2 transaction)
		{
			if (_logger.IsDebugEnabled) _logger.Debug("async case");

			var capture = Zipkin.TraceContextPropagation.CaptureCurrentTrace();
			var trace = new Zipkin.LocalTrace("tx");

			try
			{
				invocation.Proceed();

				var ret = (Task) invocation.ReturnValue;

				if (ret == null)
					throw new Exception("Async method returned null instead of Task - bad programmer somewhere");

				SafeHandleAsyncCompletion(ret, transaction, ref capture, ref trace);
			}
			catch (Exception e)
			{
				_logger.Error("Transactional call failed", e);

				// Early termination. nothing to do besides disposing the transaction
				
				transaction.Rollback();

				transaction.Dispose();

				trace.Dispose();

				throw;
			}
		}

		private void SafeHandleAsyncCompletion(Task ret, ITransaction2 transaction, ref TraceInfo capture, ref LocalTrace trace)
		{
			if (!ret.IsCompleted)
			{
				// When promised to complete in the future - should this be a case for DependentTransaction?
				// Transaction.Current.DependentClone(DependentCloneOption.BlockCommitUntilComplete));

				transaction.DetachContext();


				ret.ContinueWith((t, tupleArg) =>
				{
					// var tran = (ITransaction2) aTransaction;
					var tuple = (Tuple<ITransaction2, ILogger>) tupleArg;
					var tran = tuple.Item1;
					var logger = tuple.Item2;

					try
					{
						if (!t.IsFaulted && !t.IsCanceled && tran.State == TransactionState.Active)
						{
							try
							{
								tran.Complete();
							}
							catch (Exception e)
							{
								logger.Error("Transaction complete error ", e);
								throw;
							}
						}
						else
						{
							try
							{
								tran.Rollback();
							}
							catch (Exception e)
							{
								logger.Error("Transaction complete error ", e);
								throw;
							}
						}
					}
					finally
					{
						tran.Dispose();
					}

				}, Tuple.Create(transaction, _logger), TaskContinuationOptions.ExecuteSynchronously);
			}
			else
			{
				// When completed synchronously 

				try
				{
					if (transaction.State == TransactionState.Active && !ret.IsFaulted && !ret.IsCanceled)
					{
						transaction.Complete();
						// transaction.Dispose();
					}
					else // if (_logger.IsWarnEnabled)
					{
						transaction.Rollback();

						if (_logger.IsWarnEnabled)
						{
							_logger.WarnFormat("transaction was in state {0}, so it cannot be completed. the 'consumer' method, so to speak, might have rolled it back.",
							transaction.State);
						}
					}
				}
				finally
				{
					transaction.Dispose();
				}
			}
		}

		private void SynchronizedCase(IInvocation invocation, ITransaction2 transaction)
		{
			if (_logger.IsDebugEnabled)
				_logger.Debug("synchronized case");

			// using (new TxScope(transaction.Inner, _logger.CreateChildLogger("TxScope")))

			var localIdentifier = transaction.LocalIdentifier;
			var trace = new Zipkin.LocalTrace("tx");

			try
			{
				invocation.Proceed();

				if (transaction.State == TransactionState.Active)
				{
					transaction.Complete();
					transaction.Dispose();
				}
				else if (_logger.IsWarnEnabled)
					_logger.WarnFormat(
						"transaction was in state {0}, so it cannot be completed. the 'consumer' method, so to speak, might have rolled it back.",
						transaction.State);
			}
			catch (Exception ex)
			{
				if (_logger.IsErrorEnabled)
					_logger.Error("caught exception, rolling back transaction - synchronized case - tx#" + localIdentifier);

				trace.AnnotateWith(PredefinedTag.Error, ex.Message);

				transaction.Rollback();

				throw;
			}
			finally
			{
				if (_logger.IsDebugEnabled)
					_logger.Debug("dispoing transaction - synchronized case - tx#" + localIdentifier);

				transaction.Dispose();

				trace.Dispose();
			}
		}
	}
}
