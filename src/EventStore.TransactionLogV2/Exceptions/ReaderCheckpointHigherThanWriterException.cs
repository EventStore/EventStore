using System;

namespace EventStore.Core.TransactionLogV2.Exceptions {
	public class ReaderCheckpointHigherThanWriterException : Exception {
		public ReaderCheckpointHigherThanWriterException(string checkpointName)
			: base(string.Format("Checkpoint '{0}' has greater value than writer checkpoint.", checkpointName)) {
		}
	}
}
