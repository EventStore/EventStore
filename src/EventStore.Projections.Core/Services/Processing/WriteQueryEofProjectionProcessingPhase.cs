using System.Collections.Generic;
using EventStore.Core.Bus;

namespace EventStore.Projections.Core.Services.Processing
{
    public sealed class WriteQueryEofProjectionProcessingPhase : WriteQueryResultProjectionProcessingPhaseBase
    {
        public WriteQueryEofProjectionProcessingPhase(
            IPublisher publisher,
            int phase,
            string resultStream,
            ICoreProjectionForProcessingPhase coreProjection,
            PartitionStateCache stateCache,
            ICoreProjectionCheckpointManager checkpointManager,
            IEmittedEventWriter emittedEventWriter)
            : base(publisher, phase, resultStream, coreProjection, stateCache, checkpointManager, emittedEventWriter)
        {
        }

        protected override IEnumerable<EmittedEventEnvelope> WriteResults(CheckpointTag phaseCheckpointTag)
        {
            // do nothing
            yield break;
        }
    }
}
