using System;

namespace EventStore.Core.Exceptions {
	internal class PossibleToHandleOutOfMemoryException : OutOfMemoryException {
		public PossibleToHandleOutOfMemoryException() {
		}

		public PossibleToHandleOutOfMemoryException(string message) : base(message) {
		}

		public PossibleToHandleOutOfMemoryException(string message, Exception innerException) : base(message,
			innerException) {
		}
	}
}
