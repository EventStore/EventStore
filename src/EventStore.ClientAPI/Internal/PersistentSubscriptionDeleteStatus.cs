namespace EventStore.ClientAPI.Internal {
	/// <summary>
	/// Enumeration representing the status of a single subscription delete message.
	/// </summary>
	enum PersistentSubscriptionDeleteStatus {
		/// <summary>
		/// The subscription was created successfully
		/// </summary>
		Success = 0,

		/// <summary>
		/// Some failure happened creating the subscription
		/// </summary>
		Failure = 1,
	}
}
