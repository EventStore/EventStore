using System;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	class GetStateWorkItem : GetDataWorkItemBase {
		public GetStateWorkItem(
			IPublisher publisher,
			Guid correlationId,
			Guid projectionId,
			IProjectionPhaseStateManager projection,
			string partition)
			: base(publisher, correlationId, projectionId, projection, partition) {
		}

		protected override void Reply(PartitionState state, CheckpointTag checkpointTag) {
			if (state == null)
				_publisher.Publish(
					new CoreProjectionStatusMessage.StateReport(
						_correlationId,
						_projectionId,
						_partition,
						null,
						checkpointTag));
			else
				_publisher.Publish(
					new CoreProjectionStatusMessage.StateReport(
						_correlationId,
						_projectionId,
						_partition,
						state.State,
						checkpointTag));
		}
	}
}
