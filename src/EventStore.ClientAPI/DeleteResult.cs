namespace EventStore.ClientAPI {
	/// <summary>
	/// Result type returned after deleting a stream.
	/// </summary>
	public struct DeleteResult {
		/// <summary>
		/// The <see cref="LogPosition"/> of the write.
		/// </summary>
		public readonly Position LogPosition;

		/// <summary>
		/// Constructs a new <see cref="DeleteResult"/>.
		/// </summary>
		/// <param name="logPosition">The position of the write in the log</param>
		public DeleteResult(Position logPosition) {
			LogPosition = logPosition;
		}
	}
}
