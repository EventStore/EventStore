using EventStore.Core;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Grpc.Tests {
	public class TestVNodeBuilder : VNodeBuilder {
		protected override void SetUpProjectionsIfNeeded() {
		}

		public TFChunkDb GetDb() {
			return _db;
		}

	}
}
