using System.Threading;
using EventStore.Core.TransactionLog.Chunks;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class ChunkMerger : IChunkMerger {
		private readonly ILogger _logger;
		private readonly bool _mergeChunks;
		private readonly IChunkMergerBackend _backend;
		private readonly Throttle _throttle;

		public ChunkMerger(
			ILogger logger,
			bool mergeChunks,
			IChunkMergerBackend backend,
			Throttle throttle) {

			_logger = logger;
			_mergeChunks = mergeChunks;
			_backend = backend;
			_throttle = throttle;
		}

		public void MergeChunks(
			ScavengePoint scavengePoint,
			IScavengeStateForChunkMerger state,
			ITFChunkScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			_logger.Debug("SCAVENGING: Started new scavenge chunk merging phase for {scavengePoint}",
				scavengePoint.GetName());

			var checkpoint = new ScavengeCheckpoint.MergingChunks(scavengePoint);
			state.SetCheckpoint(checkpoint);
			MergeChunks(checkpoint, state, scavengerLogger, cancellationToken);
		}

		public void MergeChunks(
			ScavengeCheckpoint.MergingChunks checkpoint,
			IScavengeStateForChunkMerger state,
			ITFChunkScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			if (_mergeChunks) {
				_logger.Debug("SCAVENGING: Merging chunks from checkpoint: {checkpoint}", checkpoint);
				_backend.MergeChunks(scavengerLogger, _throttle, cancellationToken);
			} else {
				_logger.Debug("SCAVENGING: Merging chunks is disabled");
			}
		}
	}
}
