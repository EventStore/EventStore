namespace EventStore.ClientAPI.Internal {
	/// <summary>
	/// Enumeration representing the status of a single subscription create message.
	/// </summary>
	public enum PersistentSubscriptionUpdateStatus {
		/// <summary>
		/// The subscription was created successfully
		/// </summary>
		Success = 0,

		/// <summary>
		/// The subscription already exists
		/// </summary>
		NotFound = 1,

		/// <summary>
		/// Some failure happened creating the subscription
		/// </summary>
		Failure = 2,

		/// <summary>
		/// You do not have permissions to update this subscription
		/// </summary>
		AccessDenied = 3,
	}
}
