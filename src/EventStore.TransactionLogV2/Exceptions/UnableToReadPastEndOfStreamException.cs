using System;

namespace EventStore.Core.TransactionLogV2.Exceptions {
	internal class UnableToReadPastEndOfStreamException : Exception {
		public UnableToReadPastEndOfStreamException() {
		}

		public UnableToReadPastEndOfStreamException(string message) : base(message) {
		}

		public UnableToReadPastEndOfStreamException(string message, Exception innerException) : base(message,
			innerException) {
		}
	}
}
