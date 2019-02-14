using System;

namespace EventStore.Core.Exceptions {
	public class CorruptIndexException : Exception {
		public CorruptIndexException() {
		}

		public CorruptIndexException(Exception innerException) : base("Corrupted index.", innerException) {
		}

		public CorruptIndexException(string message) : base(message) {
		}

		public CorruptIndexException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}
