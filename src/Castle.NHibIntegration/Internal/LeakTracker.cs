namespace Castle.NHibIntegration.Internal
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Runtime.CompilerServices;
	using System.Threading;
	using Core.Logging;
	using NHibernate;
	using StatsdClient;
	using StatsdClient.Configuration;

	public class LeakTracker
	{
		private readonly ConditionalWeakTable<ISession, CreationInfo> _weakTable;
		private readonly List<WeakReference<ISession>> _sessions;
		private int _counter;
		private Timer _timer;

		public LeakTracker()
		{
			_weakTable = new ConditionalWeakTable<ISession, CreationInfo>();
			_sessions = new List<WeakReference<ISession>>();

			_timer = new Timer(OnTimer, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));

			this.Logger = NullLogger.Instance;
		}

		public ILogger Logger { get; set; }

		private void OnTimer(object state)
		{
			if (_counter == 0) return;

			lock (_sessions)
			{
				foreach (var weakReference in _sessions)
				{
					ISession session;
					if (!weakReference.TryGetTarget(out session)) 
						continue;

					CreationInfo creation;
					if (!_weakTable.TryGetValue(session, out creation)) 
						continue;

					this.Logger.ErrorFormat("Session hanging here for {0} seconds, created at {1}", 
						(DateTime.Now - creation.Created).TotalSeconds, creation.Source);
				}
			}
		}

		public void Started(ISession session, string @alias, SessionDelegate previousWrappedIfAny)
		{
			RemoveAllNulls();

			Interlocked.Increment(ref _counter);

#if DEBUG
			_weakTable.Add(session, new CreationInfo {  Created = DateTime.Now, Source = new StackTrace(), ThreadName = System.Threading.Thread.CurrentThread.Name });
#else
			_weakTable.Add(session, new CreationInfo {  Created = DateTime.Now, ThreadName = System.Threading.Thread.CurrentThread.Name });
#endif
			lock (_sessions)
				_sessions.Add(new WeakReference<ISession>(session, trackResurrection: false));

			Metrics.Gauge(Naming.withEnvironmentApplicationAndHostname("nhibernate.sessions.active"), _counter);
		}

		public void Remove(ISession session)
		{
			Interlocked.Decrement(ref _counter);

			_weakTable.Remove(session);

			lock (_sessions)
			// foreach (var weakReference in _sessions)
			for (int i = 0; i < _sessions.Count; i++)
			{
				var weakReference = _sessions[i];
				ISession dummy;
				if (weakReference.TryGetTarget(out dummy) && dummy == session)
				{
					_sessions.RemoveAt(i);
					break;
				}
			}

			Metrics.Gauge(Naming.withEnvironmentApplicationAndHostname("nhibernate.sessions.active"), _counter);
		}

		private void RemoveAllNulls()
		{
			lock (_sessions)
			for (int i = 0; i < _sessions.Count; i++)
			{
				var weakReference = _sessions[i];
				ISession dummy;
				if (!weakReference.TryGetTarget(out dummy))
				{
					_sessions.Remove(weakReference);
					break; // can only remove one, or there will be problems with the enumerator
				}
			}
		}

		class CreationInfo
		{
			public DateTime Created;

			public StackTrace Source;

			public string ThreadName { get; set; }
		}

		public void Reused(ISession innerSession, string @alias, SessionDelegate wrapped)
		{
			CreationInfo info;
			if (_weakTable.TryGetValue(innerSession, out info))
			{
				var tname = Thread.CurrentThread.Name;

				if (info.ThreadName != tname)
					Logger.WarnFormat("Session shared between threads. Session = [{SessionId}, {CreatorThread}, {CurrentThread}]", 
						innerSession.GetSessionImplementation().SessionId, info.ThreadName, tname);
			}
		}
	}
}