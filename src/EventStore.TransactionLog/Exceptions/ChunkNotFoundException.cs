using System;

namespace EventStore.Core.TransactionLog.Exceptions {
	public class ChunkNotFoundException : Exception {
		public ChunkNotFoundException(string chunkName) : base(chunkName + " not found.") {
		}
	}
}
