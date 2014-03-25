using System.Collections.Generic;

namespace EventStore.Projections.Core.Services.Processing
{
    public sealed class WriteQueryEofProjectionProcessingPhase : WriteQueryResultProjectionProcessingPhaseBase
    {
        public WriteQueryEofProjectionProcessingPhase(
            int phase, string resultStream, ICoreProjectionForProcessingPhase coreProjection,
            PartitionStateCache stateCache, ICoreProjectionCheckpointManager checkpointManager,
            IEmittedEventWriter emittedEventWriter)
            : base(phase, resultStream, coreProjection, stateCache, checkpointManager, emittedEventWriter)
        {
        }

        protected override IEnumerable<EmittedEventEnvelope> WriteResults(CheckpointTag phaseCheckpointTag)
        {
            // do nothing
            yield break;
        }
    }
}
