using System;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Result type returned after conditionally writing to a stream.
	/// </summary>
	public struct ConditionalWriteResult {
		/// <summary>
		/// Returns if the write was successful.
		/// </summary>
		public readonly ConditionalWriteStatus Status;

		/// <summary>
		/// The next expected version for the stream.
		/// </summary>
		public readonly long? NextExpectedVersion;

		/// <summary>
		/// The <see cref="LogPosition"/> of the write.
		/// </summary>
		public readonly Position? LogPosition;

		/// <summary>
		/// Constructs a new <see cref="WriteResult"/>.
		/// </summary>
		/// <param name="nextExpectedVersion">The next expected version for the stream.</param>
		/// <param name="logPosition">The position of the write in the log</param>
		public ConditionalWriteResult(long nextExpectedVersion, Position logPosition) {
			LogPosition = logPosition;
			NextExpectedVersion = nextExpectedVersion;
			Status = ConditionalWriteStatus.Succeeded;
		}

		/// <summary>
		/// Constructs a new <see cref="WriteResult"/>.
		/// </summary>
		/// <param name="status">The status of the write operation.</param>
		public ConditionalWriteResult(ConditionalWriteStatus status) {
			if (status == ConditionalWriteStatus.Succeeded) {
				throw new Exception("For successful write pass next expected version and log position.");
			}

			LogPosition = null;
			NextExpectedVersion = null;
			Status = status;
		}
	}
}
