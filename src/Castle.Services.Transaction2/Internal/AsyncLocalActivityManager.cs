namespace Castle.Services.Transaction.Internal
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;
	using Core.Logging;

	public class AsyncLocalActivityManager : IActivityManager2
	{
		private readonly AsyncLocal<Activity2> _holder;
		private int _counter;

		private ILogger _logger = NullLogger.Instance;

		public AsyncLocalActivityManager()
		{
			_holder = new AsyncLocal<Activity2>( /*OnValueChanged*/ );
		}

		public bool HasActivityWithTransaction
		{
			get
			{
				Activity2 activity;
				if (TryGetCurrentActivity(out activity))
				{
					return activity.CurrentTransaction != null;
				}
				return false;
			}
		}

		// Invoked by the activy itself after popping a transaction
		public void NotifyPop(Activity2 activity2)
		{
			// if (activity2.IsEmpty)
			{
				var ctxActivity = _holder.Value;
				if (ctxActivity != null && !activity2.Equals(ctxActivity))
				{
					// wtf?
					_logger.Fatal("activity does not match the context one. Expecting " + activity2 + " but found " + ctxActivity);
				}

				_holder.Value = null; // removes empty activity from context

				activity2.Dispose();
			}
		}

		public ILogger Logger
		{
			get { return _logger; }
			set { _logger = value; }
		}

		public Activity2 EnsureActivityExists()
		{
			var cur = _holder.Value;
			bool wecreated = false;
			if (cur == null)
			{
				_holder.Value = cur = CreateActivity();
				wecreated = true;
			}
			if (cur.IsDisposed)
			{
				var msg = cur + " already disposed. we created? " + wecreated + " [" + Thread.CurrentThread.ManagedThreadId + "_" +
				          Thread.CurrentThread.Name + "]";
				_logger.Fatal(msg);
				// throw new Exception(msg);
				_holder.Value = cur = CreateActivity();
			}
			return cur;
		}

		public bool TryGetCurrentActivity(out Activity2 activity)
		{
			activity = null;
			var cur = _holder.Value;
			if (cur == null)
				return false;
			activity = _holder.Value;
			return true;
		}

		public void Detach(Activity2 activity2)
		{
			// _logger.Fatal("Detaching : " + activity2);

			// Confirms the context and specified one are the same:

			var ctxActivity = _holder.Value;
			if (ctxActivity != null && !activity2.Equals(ctxActivity))
			{
				_logger.Fatal("Detach: activity does not match the context one. Expecting " + activity2 + " but found " + ctxActivity);	
			}

			// Remove activity from context so it cannot be reused in the chain
			_holder.Value = null; 
		}

		public void Dispose()
		{
		}

		private Activity2 CreateActivity()
		{
			var id = Interlocked.Increment(ref _counter);
			return new Activity2(this, id, _logger.CreateChildLogger("Activity"));
		}
	}
}