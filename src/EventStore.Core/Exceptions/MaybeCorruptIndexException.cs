using System;

namespace EventStore.Core.Exceptions {
	public class MaybeCorruptIndexException : Exception {
		public MaybeCorruptIndexException() {
		}

		public MaybeCorruptIndexException(Exception innerException) : base("May be corrupted index.", innerException) {
		}

		public MaybeCorruptIndexException(string message) : base(message) {
		}

		public MaybeCorruptIndexException(string message, Exception innerException) : base(message, innerException) {
		}
	}
}
