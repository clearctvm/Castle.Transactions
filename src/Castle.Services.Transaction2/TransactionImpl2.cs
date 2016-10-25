namespace Castle.Services.Transaction
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Transactions;
	using Core.Logging;
	using Internal;

	public class TransactionImpl2 : ITransaction2
	{
		private volatile TransactionState _currentState;

		private readonly CommittableTransaction _transaction;
		private readonly TransactionScope _txScope;
		private readonly Activity2 _parentActivity;
		private readonly string _localIdentifier;
		private readonly Lazy<IDictionary<string, object>> _lazyUserData;
//		private readonly ILogger _logger;
		private int _disposed;
		private bool? _shouldCommit;

		public TransactionImpl2(System.Transactions.CommittableTransaction transaction, 
								System.Transactions.TransactionScope txScope, 
								Activity2 parentActivity)
		{
			_transaction = transaction;
			_txScope = txScope;
			_parentActivity = parentActivity;
//			_logger = logger;
			_localIdentifier = transaction.TransactionInformation.LocalIdentifier;

			_currentState = TransactionState.Active;

			_lazyUserData = new Lazy<IDictionary<string, object>>(() => new Dictionary<string, object>(StringComparer.Ordinal), LazyThreadSafetyMode.ExecutionAndPublication);

			// _parentActivity.Push(this);
			_parentActivity.SetTransaction(this);
		}

		public Transaction Inner { get { return _transaction; } }
		public string LocalIdentifier { get { return _localIdentifier; } }
		public TransactionState State { get { return _currentState; } }
		public IDictionary<string, object> UserData { get { return _lazyUserData.Value; } }
		public bool HasUserData { get { return _lazyUserData.IsValueCreated; } }

		public TransactionStatus? Status
		{
			get
			{
				return _transaction != null ? 
					_transaction.TransactionInformation.Status : (TransactionStatus?) null;
			}
		}
		
		public void Rollback()
		{
			if (_disposed == 1) throw new ObjectDisposedException("Can't Rollback(). Transaction2 disposed");

			// InternalRollback();

			_shouldCommit = false;

			// _transaction.Rollback();
		}

		public void Complete()
		{
			if (_disposed == 1) throw new ObjectDisposedException("Can't Complete(). Transaction2 disposed");

//			InternalComplete();
			
			_shouldCommit = true;
		}

		public void DetachContext()
		{
			_parentActivity.Detach();
		}

		public void Dispose()
		{
			if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
				return;

			// shouldCommit when wasn't explicit set or was set to true
			var shouldCommit = !_shouldCommit.HasValue || _shouldCommit.Value == true;

			try
			{
				if (shouldCommit)
				{
					_txScope.Complete();
				}

				_txScope.Dispose(); // this does not follow the guidelines, and might throw

				if (shouldCommit)
				{
					_transaction.Commit();
				}

				Inner.Dispose();
			}
			finally
			{
				_parentActivity.UnsetTransaction(this);

				_currentState = TransactionState.Disposed;
			}
		}

		public override string ToString()
		{
			return "tx#" + _localIdentifier;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return InternalEquals((TransactionImpl2)obj);
		}

		public override int GetHashCode()
		{
			return (_localIdentifier != null ? _localIdentifier.GetHashCode() : 0);
		}

		internal bool InternalEquals(TransactionImpl2 other)
		{
			return string.Equals(_localIdentifier, other._localIdentifier);
		}
	}
}