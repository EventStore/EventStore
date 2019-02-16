using System;

namespace EventStore.Core.Exceptions {
	public class ReaderCheckpointHigherThanWriterException : Exception {
		public ReaderCheckpointHigherThanWriterException(string checkpointName)
			: base(string.Format("Checkpoint '{0}' has greater value than writer checkpoint.", checkpointName)) {
		}
	}
}
