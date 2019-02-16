namespace EventStore.ClientAPI {
	/// <summary>
	/// Result type returned after writing to a stream.
	/// </summary>
	public struct WriteResult {
		/// <summary>
		/// The next expected version for the stream.
		/// </summary>
		public readonly long NextExpectedVersion;

		/// <summary>
		/// The <see cref="LogPosition"/> of the write.
		/// </summary>
		public readonly Position LogPosition;

		/// <summary>
		/// Constructs a new <see cref="WriteResult"/>.
		/// </summary>
		/// <param name="nextExpectedVersion">The next expected version for the stream.</param>
		/// <param name="logPosition">The position of the write in the log</param>
		public WriteResult(long nextExpectedVersion, Position logPosition) {
			LogPosition = logPosition;
			NextExpectedVersion = nextExpectedVersion;
		}
	}
}
