using System;

namespace EventStore.Core.TransactionLogV2.Exceptions {
	public class ChunkNotFoundException : Exception {
		public ChunkNotFoundException(string chunkName) : base(chunkName + " not found.") {
		}
	}
}
