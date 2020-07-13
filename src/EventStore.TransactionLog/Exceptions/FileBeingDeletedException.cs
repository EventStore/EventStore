using System;

namespace EventStore.Core.TransactionLog.Exceptions {
	public class FileBeingDeletedException : Exception {
		public FileBeingDeletedException() {
		}

		public FileBeingDeletedException(string message) : base(message) {
		}

		public FileBeingDeletedException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}
