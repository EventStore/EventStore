using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.TransactionLog.Scavenging {
	public interface IChunkMergerBackend {
		ValueTask MergeChunks(
			ITFChunkScavengerLog scavengerLogger,
			Throttle throttle,
			CancellationToken cancellationToken);
	}
}
