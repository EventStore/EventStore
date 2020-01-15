namespace EventStore.Client.PersistentSubscriptions {
	/// <summary>
	/// System supported consumer strategies for use with persistent subscriptions.
	/// </summary>
	public static class SystemConsumerStrategies {
		/// <summary>
		/// Distributes events to a single client until it is full. Then round robin to the next client.
		/// </summary>
		public const string DispatchToSingle = nameof(DispatchToSingle);

		/// <summary>
		/// Distribute events to each client in a round robin fashion.
		/// </summary>
		public const string RoundRobin = nameof(RoundRobin);

		/// <summary>
		/// Distribute events of the same streamId to the same client until it disconnects on a best efforts basis. 
		/// Designed to be used with indexes such as the category projection.
		/// </summary>
		public const string Pinned = nameof(Pinned);
	}
}
