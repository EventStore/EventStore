using System;

namespace EventStore.Core.TransactionLog.Exceptions {
	public class BadChunkInDatabaseException : Exception {
		public BadChunkInDatabaseException(string message) : base(message) {
		}
	}
}
