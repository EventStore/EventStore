using System;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Services.Processing
{
    abstract class GetDataWorkItemBase : WorkItem
    {
        protected readonly string _partition;
        protected readonly IEnvelope _envelope;
        protected Guid _correlationId;
        protected Guid _projectionId;
        private readonly IProjectionPhaseStateManager _projection;
        private PartitionState _state;
        private CheckpointTag _lastProcessedCheckpointTag;

        protected GetDataWorkItemBase(
            IEnvelope envelope, Guid correlationId, Guid projectionId, IProjectionPhaseStateManager projection, string partition)
            : base(null)
        {
            if (envelope == null) throw new ArgumentNullException("envelope");
            if (partition == null) throw new ArgumentNullException("partition");
            _partition = partition;
            _envelope = envelope;
            _correlationId = correlationId;
            _projectionId = projectionId;
            _projection = projection;
        }

        protected override void GetStatePartition()
        {
            NextStage(_partition);
        }

        protected override void Load(CheckpointTag checkpointTag)
        {
            _lastProcessedCheckpointTag = _projection.LastProcessedEventPosition;
            _projection.BeginGetPartitionStateAt(
                _partition, _lastProcessedCheckpointTag, LoadCompleted, lockLoaded: false);
        }

        private void LoadCompleted(PartitionState state)
        {
            _state = state;
            NextStage();
        }

        protected override void WriteOutput()
        {
            Reply(_state, _lastProcessedCheckpointTag);
            NextStage();
        }

        protected abstract void Reply(PartitionState state, CheckpointTag checkpointTag);
    }
}
