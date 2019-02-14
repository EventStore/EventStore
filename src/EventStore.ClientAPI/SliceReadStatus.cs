namespace EventStore.ClientAPI {
	/// <summary>
	/// Enumeration detailing the possible outcomes of reading a 
	/// slice of a stream.
	/// </summary>
	public enum SliceReadStatus {
		/// <summary>
		/// The read was successful.
		/// </summary>
		Success,

		/// <summary>
		/// The stream was not found.
		/// </summary>
		StreamNotFound,

		/// <summary>
		/// The stream has previously existed but is deleted.
		/// </summary>
		StreamDeleted
	}
}
