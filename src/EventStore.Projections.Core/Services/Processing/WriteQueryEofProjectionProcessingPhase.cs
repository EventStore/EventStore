using System.Collections.Generic;
using EventStore.Core.Bus;

namespace EventStore.Projections.Core.Services.Processing {
	public sealed class WriteQueryEofProjectionProcessingPhase : WriteQueryResultProjectionProcessingPhaseBase {
		public WriteQueryEofProjectionProcessingPhase(
			IPublisher publisher,
			int phase,
			string resultStream,
			ICoreProjectionForProcessingPhase coreProjection,
			PartitionStateCache stateCache,
			ICoreProjectionCheckpointManager checkpointManager,
			IEmittedEventWriter emittedEventWriter,
			IEmittedStreamsTracker emittedStreamsTracker)
			: base(publisher, phase, resultStream, coreProjection, stateCache, checkpointManager, emittedEventWriter,
				emittedStreamsTracker) {
		}

		protected override IEnumerable<EmittedEventEnvelope> WriteResults(CheckpointTag phaseCheckpointTag) {
			// do nothing
			yield break;
		}
	}
}
