using System;

namespace EventStore.Core.TransactionLogV2.Exceptions {
	public class InvalidFileException : Exception {
		public InvalidFileException() {
		}

		public InvalidFileException(string message) : base(message) {
		}

		public InvalidFileException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}
