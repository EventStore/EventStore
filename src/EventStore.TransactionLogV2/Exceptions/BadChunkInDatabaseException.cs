using System;

namespace EventStore.Core.TransactionLogV2.Exceptions {
	public class BadChunkInDatabaseException : Exception {
		public BadChunkInDatabaseException(string message) : base(message) {
		}
	}
}
