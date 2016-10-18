﻿namespace Castle.Services.Transaction.Internal
{
	public interface IActivityManager2
	{
		bool TryGetCurrentActivity(out Activity2 activity);

		Activity2 EnsureActivityExists();
		
		void NotifyPop(Activity2 activity2);

		void Dispose();
		
		void Detach(Activity2 activity2);

		bool HasActivityWithTransaction { get;  }
	}
}