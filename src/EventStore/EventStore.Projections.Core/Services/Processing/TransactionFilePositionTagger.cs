using System;

namespace EventStore.Projections.Core.Services.Processing
{
    public class TransactionFilePositionTagger : PositionTagger
    {
        public override bool IsCompatible(CheckpointTag checkpointTag)
        {
            return checkpointTag.GetMode() == CheckpointTag.Mode.Position;
        }
    }
}