namespace EventStore.ClientAPI {
	/// <summary>
	/// Represents the direction of read operation (both from $all and usual streams)
	/// </summary>
	public enum ReadDirection {
		/// <summary>
		/// From beginning to end.
		/// </summary>
		Forward,

		/// <summary>
		/// From end to beginning.
		/// </summary>
		Backward
	}
}
