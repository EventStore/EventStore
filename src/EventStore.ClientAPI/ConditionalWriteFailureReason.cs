namespace EventStore.ClientAPI {
	/// <summary>
	/// The reason why a conditional write fails
	/// </summary>
	public enum ConditionalWriteStatus {
		/// <summary>
		/// The write operation succeeded
		/// </summary>
		Succeeded = 0,

		/// <summary>
		/// The expected version does not match actual stream version
		/// </summary>
		VersionMismatch = 1,

		/// <summary>
		/// The stream has been deleted
		/// </summary>
		StreamDeleted = 2
	}
}
