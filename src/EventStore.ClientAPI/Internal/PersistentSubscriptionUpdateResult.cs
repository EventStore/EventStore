namespace EventStore.ClientAPI.Internal {
	/// <summary>
	/// A Persistent Subscription Create Result is the result of a single operation updating a
	/// persistent subscription in the event store
	/// </summary>
	public class PersistentSubscriptionUpdateResult {
		/// <summary>
		/// The <see cref="PersistentSubscriptionUpdateResult"/> representing the status of this create attempt
		/// </summary>
		public readonly PersistentSubscriptionUpdateStatus Status;

		internal PersistentSubscriptionUpdateResult(PersistentSubscriptionUpdateStatus status) {
			Status = status;
		}
	}
}
