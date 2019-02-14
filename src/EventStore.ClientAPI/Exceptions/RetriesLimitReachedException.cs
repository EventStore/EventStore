namespace EventStore.ClientAPI.Exceptions {
	/// <summary>
	/// Exception thrown if the number of retries for an operation is reached.
	/// To change the number of retries attempted for an operation, use the methods
	/// <see cref="ConnectionSettingsBuilder.LimitRetriesForOperationTo" /> or 
	/// <see cref="ConnectionSettingsBuilder.KeepRetrying" /> and pass the resulting
	/// <see cref="ConnectionSettings" /> into the constructor of the connection.
	/// </summary>
	public class RetriesLimitReachedException : EventStoreConnectionException {
		/// <summary>
		/// Constructs a new instance of <see cref="RetriesLimitReachedException"/>.
		/// </summary>
		/// <param name="retries">The number of retries attempted.</param>
		public RetriesLimitReachedException(int retries)
			: base(string.Format("Reached retries limit : {0}", retries)) {
		}

		/// <summary>
		/// Constructs a new instance of <see cref="RetriesLimitReachedException"/>.
		/// </summary>
		/// <param name="item">The name of the item for which retries were attempted.</param>
		/// <param name="retries">The number of retries attempted.</param>
		public RetriesLimitReachedException(string item, int retries)
			: base(string.Format("Item {0} reached retries limit : {1}", item, retries)) {
		}
	}
}
