using System.Threading;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IChunkMergerBackend {
		void MergeChunks(
			ITFChunkScavengerLog scavengerLogger,
			Throttle throttle,
			CancellationToken cancellationToken);
	}
}
