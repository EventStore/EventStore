namespace EventStore.ClientAPI {
	/// <summary>
	/// Enumeration representing the status of a single event read operation.
	/// </summary>
	public enum EventReadStatus {
		/// <summary>
		/// The read operation was successful.
		/// </summary>
		Success = 0,

		/// <summary>
		/// The event was not found.
		/// </summary>
		NotFound = 1,

		/// <summary>
		/// The stream was not found.
		/// </summary>
		NoStream = 2,

		/// <summary>
		/// The stream previously existed but was deleted.
		/// </summary>
		StreamDeleted = 3,
	}
}
