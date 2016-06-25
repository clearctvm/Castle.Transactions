namespace Castle.NHibIntegration.Tests.Comps
{
	using System;
	using System.Threading;
	using FluentAssertions;
	using Services.Transaction;

	public class SvcAutoWithTransactions
	{
		private readonly ISessionManager _sessionManager;

		public SvcAutoWithTransactions(ISessionManager sessionManager)
		{
			_sessionManager = sessionManager;
		}

		[AutoCloseSession]
		public virtual void ExplicitSessionManagement()
		{
			using (var sess = _sessionManager.OpenSession())
			{
				Child1();
				
				Thread.Sleep(1000);

				var isOpen = sess.IsOpen;
				isOpen.Should().BeTrue();
			}
		}

		[AutoCloseSession]
		public virtual void NoExplicitSessionManagement()
		{
			var sess = _sessionManager.OpenSession();
			{
				Child1();

				Thread.Sleep(1000);

				var isOpen = sess.IsOpen;
				isOpen.Should().BeTrue();
			}
		}

		[Transaction]
		public virtual void TransactionThenAutoClose()
		{
			var sess = _sessionManager.OpenSession();
			{
				ChildAutoClose();

				var isOpen = sess.IsOpen;
			}
		}

		[Transaction]
		public virtual void Child1()
		{
			using (var sess = _sessionManager.OpenSession())
			{
				Child2();

				var isOpen = sess.IsOpen;
			}
		}

		[Transaction]
		public virtual void Child2()
		{
			using (var sess = _sessionManager.OpenSession())
			{
				var isOpen = sess.IsOpen;

				sess.Save(new TestTable { Id = Guid.NewGuid(), Counter = 1 });

				sess.Flush();
			}
		}

		[AutoCloseSession]
		public virtual void ChildAutoClose()
		{
			using (var sess = _sessionManager.OpenSession())
			{
				var isOpen = sess.IsOpen;

				sess.Save(new TestTable { Id = Guid.NewGuid(), Counter = 1 });

				sess.Flush();
			}
		}
	}
}