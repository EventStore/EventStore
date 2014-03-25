using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    class GetStateWorkItem : GetDataWorkItemBase
    {
        public GetStateWorkItem(
            IEnvelope envelope, Guid correlationId, Guid projectionId, IProjectionPhaseStateManager projection,
            string partition)
            : base(envelope, correlationId, projectionId, projection, partition)
        {
        }

        protected override void Reply(PartitionState state, CheckpointTag checkpointTag)
        {
            if (state == null)
                _envelope.ReplyWith(
                    new CoreProjectionManagementMessage.StateReport(
                        _correlationId, _projectionId, _partition, null, checkpointTag));
            else
                _envelope.ReplyWith(
                    new CoreProjectionManagementMessage.StateReport(
                        _correlationId, _projectionId, _partition, state.State, checkpointTag));
        }
    }
}
