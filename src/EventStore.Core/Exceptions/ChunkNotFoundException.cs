using System;

namespace EventStore.Core.Exceptions {
	public class ChunkNotFoundException : Exception {
		public ChunkNotFoundException(string chunkName) : base(chunkName + " not found.") {
		}
	}
}
