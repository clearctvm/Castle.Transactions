namespace Castle.Services.Transaction2.Tests
{
	using System;
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

		public async Task CallWithDetach()
		{
			var activity = _manager.EnsureActivityExists();

			var task = FakeTransactionalCall();

			Console.WriteLine("CallChainWithPop " + Thread.CurrentThread.ManagedThreadId);

			
			activity.Detach();


			task.ContinueWith((t,_) =>
			{
				Console.WriteLine("ContinueWith " + Thread.CurrentThread.ManagedThreadId);

			}, null, TaskContinuationOptions.ExecuteSynchronously);

			await task;

			Console.WriteLine("CallChainWithPop after " + Thread.CurrentThread.ManagedThreadId);

			var activity2 = _manager.EnsureActivityExists();

			activity2.Should().NotBe(activity);
		}

		private async Task FakeTransactionalCall()
		{
			await Task.Delay(100);


		}
	}
}