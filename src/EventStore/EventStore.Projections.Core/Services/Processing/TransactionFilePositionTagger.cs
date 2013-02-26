using System;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class TransactionFilePositionTagger : PositionTagger
    {
        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            return checkpointTag.Mode_ == CheckpointTag.Mode.Position;
        }

        public override bool IsMessageAfterCheckpointTag(CheckpointTag previous, ProjectionCoreServiceMessage.CommittedEventDistributed comittedEvent)
        {
            if (previous.Mode_ != CheckpointTag.Mode.Position)
                throw new ArgumentException("Mode.Position expected", "previous");
            return comittedEvent.Position > previous.Position;
        }

        public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ProjectionCoreServiceMessage.CommittedEventDistributed comittedEvent)
        {
            return new CheckpointTag(comittedEvent.Position);
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return new CheckpointTag(new EventPosition(0, -1));
        }
    }
}