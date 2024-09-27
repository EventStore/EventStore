using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IChunkMerger {
		ValueTask MergeChunks(
			ScavengePoint scavengePoint,
			IScavengeStateForChunkMerger state,
			ITFChunkScavengerLog scavengerLogger,
			CancellationToken cancellationToken);

		ValueTask MergeChunks(
			ScavengeCheckpoint.MergingChunks checkpoint,
			IScavengeStateForChunkMerger state,
			ITFChunkScavengerLog scavengerLogger,
			CancellationToken cancellationToken);
	}
}
