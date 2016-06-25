namespace Castle.Services.Transaction2.Tests
{
	using System.Threading;
	using System.Threading.Tasks;
	using FluentAssertions;
	using Transaction.Internal;

	class TplFriendlyClass2
	{
		private readonly IActivityManager2 _manager;

		public TplFriendlyClass2(IActivityManager2 manager)
		{
			_manager = manager;
		}

		public async Task CallChainWithPop()
		{
			await FakeTransactionalCall();
		}

		private Task FakeTransactionalCall()
		{
			return null;
		}
	}

	class TplFriendlyClass
	{
		private readonly IActivityManager2 _manager;

		public TplFriendlyClass(IActivityManager2 manager)
		{
			_manager = manager;
		}

		public async Task CallWithAsyncReusingThread()
		{
			var activity = _manager.EnsureActivityExists();

			await SecondCall(activity);
			await AsyncCall(activity);

			ConfirmActivityValid(activity);

			var control = ExecutionContext.SuppressFlow();
			Task resultForOther = null;
			try
			{
				resultForOther = Task.Run(() => CallWithoutActivity());
			}
			finally
			{
				control.Undo();
			}

			resultForOther.Wait();

			ConfirmActivityValid(activity);
		}

		private Task CallWithoutActivity()
		{
			ConfirmNoActivity();

			return Task.CompletedTask;
		}

		private async Task AsyncCall(Activity2 activity)
		{
			ConfirmActivityValid(activity);

			var thread = Thread.CurrentThread.ManagedThreadId;

			// this forces a thread switch
			await Task.Delay(1000);

			// Confirm the switch
			Thread.CurrentThread.ManagedThreadId.Should().NotBe(thread);

			ConfirmActivityValid(activity);
		}

		private Task SecondCall(Activity2 activity)
		{
			ConfirmActivityValid(activity);
			return Task.CompletedTask;
		}

		private void ConfirmNoActivity()
		{
			Activity2 current;
			var res = _manager.TryGetCurrentActivity(out current);
			res.Should().BeFalse();
		}

		private void ConfirmActivityValid(Activity2 activity)
		{
			Activity2 current;
			var res = _manager.TryGetCurrentActivity(out current);
			res.Should().BeTrue();
			current.Should().Be(activity);
		}
	}
}