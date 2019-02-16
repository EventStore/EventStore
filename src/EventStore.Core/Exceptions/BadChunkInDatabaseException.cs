using System;

namespace EventStore.Core.Exceptions {
	public class BadChunkInDatabaseException : Exception {
		public BadChunkInDatabaseException(string message) : base(message) {
		}
	}
}
