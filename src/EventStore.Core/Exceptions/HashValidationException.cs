using System;

namespace EventStore.Core.Exceptions {
	public class HashValidationException : Exception {
		public HashValidationException() {
		}

		public HashValidationException(string message) : base(message) {
		}

		public HashValidationException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}
