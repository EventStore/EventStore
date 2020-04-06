using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public abstract class PositionTagger {
		public readonly int Phase;

		public PositionTagger(int phase) {
			Phase = phase;
		}

		public abstract bool IsMessageAfterCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent);

		public abstract CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent);

		public virtual CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, long eventNumber, TFPos position, string eventStreamId) {
			return CheckpointTag.Empty;
		}

		public abstract CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof);

		public abstract CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionDeleted partitionDeleted);

		public abstract CheckpointTag MakeZeroCheckpointTag();

		public abstract bool IsCompatible(CheckpointTag checkpointTag);

		public abstract CheckpointTag AdjustTag(CheckpointTag tag);
	}
}
