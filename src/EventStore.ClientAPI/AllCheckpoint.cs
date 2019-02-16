namespace EventStore.ClientAPI {
	/// <summary>
	/// This class contains constants to be used when setting up subscriptions
	/// using the  IEventStoreConnection.SubscribeToAllFrom method on
	/// <see cref="IEventStoreConnection" />.
	/// </summary>
	public static class AllCheckpoint {
		/// <summary>
		/// Indicates that a catch-up subscription should receive all events
		/// in the database.
		/// </summary>
		public static Position? AllStart = null;
	}
}
