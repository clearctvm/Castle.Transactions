namespace Castle.Services.Transaction.Internal
{
	using System;
	using System.Collections.Concurrent;
	using System.Diagnostics;
	using System.Runtime.Remoting.Messaging;
	using System.Threading;
	using Core.Logging;


	/// <summary>
	/// Inspired by the way Transaction.cs handles async support
	/// </summary>
	public class CallCtxActivityManager2 : IActivityManager2
	{
		private readonly ILogger _logger;

		private ContextDataKeeper _contextData;

		public CallCtxActivityManager2() : this(new ConsoleLogger(LoggerLevel.Debug))
		{
		}

		public CallCtxActivityManager2(ILogger logger)
		{
			_logger = logger;

			_contextData = new ContextDataKeeper(this, logger);
		}

		public Activity2 EnsureActivityExists()
		{
			var activity = _contextData.GetOrAdd();

			if (activity.IsDisposed)
			{
				var msg = "EnsureActivityExists: got an Activity already disposed! " + activity;
				this._logger.Fatal(msg);
				throw new Exception(msg);
			}

			return activity;
		}

		public bool TryGetCurrentActivity(out Activity2 activity)
		{
			var current = _contextData.GetCurrent();
			activity = current;
			if (activity != null && activity.IsDisposed)
			{
				var msg = "TryGetCurrentActivity: got an Activity already disposed! " + activity;
				this._logger.Fatal(msg);
				// throw new Exception(msg);
			}
			return current != null;
		}

		public void NotifyPop(Activity2 activity2)
		{
			if (activity2.IsEmpty) // last pop
			{
				var current = _contextData.GetCurrent();

				if (current != null && !activity2.Equals(current))
				{
					_logger.Fatal("activity does not match the context one. " +
								  "Expecting " + activity2 + " but found " + current);
				}
			
				_contextData.Free(activity2);

				activity2.Dispose();
			}
		}

		public void Detach(Activity2 activity2)
		{
			if (_logger.IsInfoEnabled)
				_logger.Info("Detaching : " + activity2);

			// Confirms the context and specified one are the same:
			var current = _contextData.GetCurrent();
			if (current != null && !activity2.Equals(current))
			{
				_logger.Fatal("Detach: activity does not match the context one. " +
							  "Expecting " + activity2 + " but found " + current);
			}

			// Remove activity from context so it cannot be reused in the chain
			_contextData.Free(activity2);
		}

		public void Dispose()
		{
			_contextData.FreeAll();
		}

		internal class ContextDataKeeper
		{
			private const string Key = "_activity";

			private readonly ConcurrentDictionary<string, Activity2> _id2Activity;
			private readonly CallCtxActivityManager2 _manager;
			private readonly ILogger _logger;
			private int _counter;

			public ContextDataKeeper(CallCtxActivityManager2 manager, ILogger logger)
			{
				_manager = manager;
				_logger = logger;
				_id2Activity = new ConcurrentDictionary<string, Activity2>(StringComparer.Ordinal);
			}

			public Activity2 GetCurrent()
			{
				Activity2 activity = null;
				var currentActivityId = (string) CallContext.LogicalGetData(Key);
				if (currentActivityId == null)
				{
					return null;
				}
				if (!_id2Activity.TryGetValue(currentActivityId, out activity))
				{
					// Should never happen!
					var message = "ContextDataKeeper: TryGetValue returned false for " + currentActivityId;
					_logger.Fatal(message);
					// throw new Exception(message);
					return null;
				}
				return activity;
			}

			public Activity2 GetOrAdd()
			{
				Activity2 activity = null;

				var currentActivityId = (string) CallContext.LogicalGetData(Key);
				
				if (currentActivityId == null)
				{
					activity = CreateAndAdd();

					if (_logger.IsDebugEnabled)
						_logger.Debug("ContextDataKeeper: created activity." + activity._id + " and set key " + CallContext.LogicalGetData(Key));
				}
				else
				{
					if (!_id2Activity.TryGetValue(currentActivityId, out activity))
					{
						// throw new Exception("Invalid state - 79");

						activity = CreateAndAdd();

						_logger.Fatal("ContextDataKeeper: there's a key in the context [" + 
							currentActivityId + "], but the dict is empty? Created instead " + activity + " at " + new StackTrace());
					}
				}

				return activity;
			}

			private Activity2 CreateAndAdd()
			{
				var id = Interlocked.Increment(ref _counter);
				var idAsStr = id.ToString();

				var activity = new Activity2(_manager, id, _logger);

				CallContext.LogicalSetData(Key, idAsStr);

				if (!_id2Activity.TryAdd(idAsStr, activity))
				{
					// Should never happen!
					_logger.Fatal("ContextDataKeeper: TryAdd returned false for " + id);
					throw new Exception("Invalid state - 78");
				}

				return activity;
			}

			// Should not call dispose on activity!
			public void Free(Activity2 activity2)
			{
				var currentActivityId = (string) CallContext.LogicalGetData(Key);
				if (currentActivityId != null)
				{
					if (currentActivityId == activity2._id.ToString())
					{
						CallContext.LogicalSetData(Key, null);
					}
					else
					{
						_logger.Fatal("Free: existing context doesnt match activity given: " + currentActivityId + " " + activity2);
					}
				}

				Activity2 existing;
				if (!_id2Activity.TryRemove(activity2._id.ToString(), out existing))
				{
					if (_logger.IsWarnEnabled)
						_logger.Warn("Free: dict said no entry for _id found " + activity2);
				}
			}

			public void FreeAll()
			{
				CallContext.LogicalSetData(Key, null);

				foreach (var kv in _id2Activity)
				{
					kv.Value.Dispose();
				}

				_id2Activity.Clear();
			}
		}
	}
}