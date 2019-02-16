namespace EventStore.ClientAPI {
	/// <summary>
	/// Constants for stream positions
	/// </summary>
	public static class StreamPosition {
		/// <summary>
		/// The first event in a stream
		/// </summary>
		public const int Start = 0;

		/// <summary>
		/// The last event in the stream.
		/// </summary>
		public const int End = -1;
	}
}
