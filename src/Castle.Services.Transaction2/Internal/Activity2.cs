﻿namespace Castle.Services.Transaction.Internal
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Threading;
	using Core.Logging;
	using Transaction;


	public class Activity2 : IDisposable
	{
		private readonly IActivityManager2 _manager;
		internal readonly int _id;
//		private readonly ILogger _logger;
//		private Stack<ITransaction2> _stack;
		private volatile bool _disposed;
		private volatile ITransaction2 _transaction;

		public Activity2(IActivityManager2 manager, int id, ILogger logger)
		{
			_manager = manager;
			_id = id;
//			_logger = logger;
//			_stack = new Stack<ITransaction2>();
		}

		public bool IsDisposed { get { return _disposed; } }

		public ITransaction2 CurrentTransaction
		{
			get
			{
//				while(_stack.Count != 0)
//				{
//					return _stack.Peek();
//				}
//				return null;

				return _transaction;
			}
		}

//		public int Count
//		{
//			get { return _stack.Count; }
//		}
//
//		public bool IsEmpty
//		{
//			get { return _stack.Count == 0; }
//		}

		public void Detach()
		{
			_manager.Detach(this);
		}

		public void SetTransaction(ITransaction2 transaction)
		{
			if (_disposed) throw new ObjectDisposedException("Activity2");

			_transaction = transaction;
		}

		public void UnsetTransaction(ITransaction2 transaction)
		{
			if (_disposed) throw new ObjectDisposedException("Activity2");

			if (!_transaction.Equals(transaction))
			{
				throw new Exception("not same");
			}

			_transaction = null;

			_manager.NotifyPop(this);
		}

//		public void Push(ITransaction2 transaction)
//		{
//			if (_disposed) throw new ObjectDisposedException("Activity2");
//
//			_stack.Push(transaction);
//
//			if (_logger.IsDebugEnabled)
//			{
//				_logger.Debug("Pushed " + transaction);
//			}
//		}
//
//		public void Pop(ITransaction2 transaction)
//		{
//			if (_disposed) throw new ObjectDisposedException("Activity2");
//
////			tryAgain:
//
////			ITransaction2 result = _stack.Pop();
//			if (_stack.Count != 0)
//			{
//				ITransaction2 result = _stack.Pop();
//				// confirm it's the expected one
//				if (!transaction.Equals(result))
//				{
//					var msg = "Transaction popped from activity didn't match the parameter one. " +
//					          "Found " + result + " and was expecting " + transaction;
//
//					_logger.Fatal(msg);
//
//					throw new Exception(msg);
//				}
//			}
//			// else if (_stack.IsEmpty)
//			else
//			{
//				var msg = "Tried to pop transaction from activity, but activity stack was empty.";
//				
//				_logger.Fatal(msg);
//
//				throw new Exception(msg);
//			}
////			else
////			{
////				goto tryAgain;
////			}
//
//			if (_logger.IsDebugEnabled)
//			{
//				_logger.Debug("Pop " + transaction);
//			}
//
//			_manager.NotifyPop(this);
//		}

		public override string ToString()
		{
			return "Activity." + _id;
		}

		protected bool InternalEquals(Activity2 other)
		{
			return _id == other._id;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return InternalEquals((Activity2)obj);
		}

		public override int GetHashCode()
		{
			return _id;
		}

		public void Dispose()
		{
//			Console.WriteLine("Disposing " + this + " [" + Thread.CurrentThread.ManagedThreadId + "_" + Thread.CurrentThread.Name + "] (** " + new StackTrace().ToString() + " **)");

			if (_disposed) return;

			_disposed = true;
			Thread.MemoryBarrier();
		}
	}
}