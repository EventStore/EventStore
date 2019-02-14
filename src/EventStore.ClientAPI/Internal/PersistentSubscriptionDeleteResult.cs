namespace EventStore.ClientAPI.Internal {
	/// <summary>
	/// A Persistent Subscription Delete Result is the result of a single operation deleting a
	/// persistent subscription in the event store
	/// </summary>
	class PersistentSubscriptionDeleteResult {
		/// <summary>
		/// The <see cref="PersistentSubscriptionDeleteStatus"/> representing the status of this create attempt
		/// </summary>
		public readonly PersistentSubscriptionDeleteStatus Status;

		internal PersistentSubscriptionDeleteResult(PersistentSubscriptionDeleteStatus status) {
			Status = status;
		}
	}
}
