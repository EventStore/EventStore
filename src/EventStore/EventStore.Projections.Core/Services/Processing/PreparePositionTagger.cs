using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class PreparePositionTagger : PositionTagger
    {
        public override bool IsMessageAfterCheckpointTag(CheckpointTag previous, ProjectionCoreServiceMessage.CommittedEventDistributed comittedEvent)
        {
            return comittedEvent.Position.PreparePosition > previous.PreparePosition;
        }

        public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ProjectionCoreServiceMessage.CommittedEventDistributed comittedEvent)
        {
            return CheckpointTag.FromPreparePosition(comittedEvent.Position.PreparePosition);
        }

        public override CheckpointTag MakeZeroCheckpointTag()
        {
            return CheckpointTag.FromPreparePosition(-1);
        }

        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            return checkpointTag.GetMode() == CheckpointTag.Mode.PreparePosition;
        }
    }
}