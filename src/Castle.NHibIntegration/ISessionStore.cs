namespace Castle.NHibIntegration
{
	using System;
	using Internal;

	/// <summary>
	/// Provides the contract for implementors who want to 
	/// store valid session so they can be reused in a invocation
	/// chain.
	/// </summary>
	public interface ISessionStore
	{
		/// <summary>
		/// Should return a previously stored session 
		/// for the given alias if available, otherwise null.
		/// </summary>
		SessionDelegate FindCompatibleSession(string alias);
		StatelessSessionDelegate FindCompatibleStatelessSession(string alias);

		/// <summary>
		/// Should store the specified session instance 
		/// </summary>
		void Store(string alias, SessionDelegate session, out Action undoAction);
		void Store(string alias, StatelessSessionDelegate session, out Action undoAction);

		int TotalStoredCurrent { get; }
		int TotalStatelessStoredCurrent { get; }

		void DisposeAllInCurrentContext();
		void DisposeAllInCurrentContext();
	}
}
