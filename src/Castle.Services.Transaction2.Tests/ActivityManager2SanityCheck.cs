namespace Castle.Services.Transaction2.Tests
{
	using System;
	using System.Threading.Tasks;
	using FluentAssertions;
	using NUnit.Framework;
	using Transaction.Internal;

	public class ActivityManager2SanityCheck<T> where T : IActivityManager2, new()
	{
		protected T _manager;

		[SetUp]
		public virtual void Init()
		{
			_manager = new T();
		}

		[TearDown]
		public virtual void TearDown()
		{
			_manager.Dispose();
		}

		[Test]
		public void NewContext_should_be_clean()
		{
			// Act
			Activity2 activity;
			var result = _manager.TryGetCurrentActivity(out activity);

			// Assert
			result.Should().BeFalse();
			activity.Should().BeNull();
		}

		[Test]
		public void Multiples_EnsureActivityExists_should_return_same()
		{
			// Arrange 

			// Act
			var activity1 = _manager.EnsureActivityExists();
			var activity2 = _manager.EnsureActivityExists();
			var activity3 = _manager.EnsureActivityExists();
			var activity4 = _manager.EnsureActivityExists();

			// Assert
			activity1.Should().NotBeNull();
			activity2.Should().NotBeNull();
			activity3.Should().NotBeNull();
			activity4.Should().NotBeNull();
			activity1.Should().Be(activity2);
			activity2.Should().Be(activity3);
			activity3.Should().Be(activity4);
		}

		[Test]
		public void TryGetCurrentActivity_should_return_existing()
		{
			// Arrange 
			var firstActivity = _manager.EnsureActivityExists();

			// Act
			Activity2 activity;
			var result = _manager.TryGetCurrentActivity(out activity);

			// Assert
			result.Should().BeTrue();
			activity.Should().NotBeNull();
			firstActivity.Should().Be(activity);
		}

		[Test]
		public void NotifyPop_should_return_free_and_dispose_activity()
		{
			// Arrange 
			var activity = _manager.EnsureActivityExists();

			// Act
			_manager.NotifyPop(activity);

			// Assert
			activity.IsDisposed.Should().BeTrue();
			Activity2 second;
			_manager.TryGetCurrentActivity(out second).Should().BeFalse();
		}

		[Test]
		public async Task AsyncCallChain()
		{
			var tplFriendlyClass = new TplFriendlyClass(_manager);

			await tplFriendlyClass.CallWithAsyncReusingThread();
		}
	}

	[TestFixture]
	public class CallCtxActivityManager2SanityTest : ActivityManager2SanityCheck<CallCtxActivityManager2>
	{
		
	}

	[TestFixture]
	public class AsyncLocalActivityManagerSanityTest : ActivityManager2SanityCheck<AsyncLocalActivityManager>
	{

	}
}