namespace EventStore.ClientAPI {
	/// <summary>
	/// Actions to be taken by server in the case of a client NAK
	/// </summary>
	public enum PersistentSubscriptionNakEventAction {
		/// <summary>
		/// Client unknown on action. Let server decide
		/// </summary>
		Unknown = 0,

		/// <summary>
		/// Park message do not resend. Put on poison queue
		/// </summary>
		Park = 1,

		/// <summary>
		/// Explicitly retry the message.
		/// </summary>
		Retry = 2,

		/// <summary>
		/// Skip this message do not resend do not put in poison queue
		/// </summary>
		Skip = 3,

		/// <summary>
		/// Stop the subscription.
		/// </summary>
		Stop = 4
	}
}
