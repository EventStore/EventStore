namespace EventStore.ClientAPI {
	/// <summary>
	/// This class contains constants to be used when setting up subscriptions
	/// using the  IEventStoreConnection.SubscribeToStreamFrom method
	/// on <see cref="IEventStoreConnection" />.
	/// </summary>
	public static class StreamCheckpoint {
		/// <summary>
		/// Indicates that a catch-up subscription should receive all events
		/// in the stream.
		/// </summary>
		public static long? StreamStart = null;
	}
}
