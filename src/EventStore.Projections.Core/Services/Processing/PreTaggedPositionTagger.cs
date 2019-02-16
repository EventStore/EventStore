using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class PreTaggedPositionTagger : PositionTagger {
		private readonly CheckpointTag _zeroCheckpointTag;

		public PreTaggedPositionTagger(int phase, CheckpointTag zeroCheckpointTag)
			: base(phase) {
			_zeroCheckpointTag = zeroCheckpointTag;
		}

		public override bool IsMessageAfterCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent) {
			if (committedEvent.PreTagged == null)
				throw new ArgumentException("committedEvent.PreTagged == null", "committedEvent");
			if (previous.Phase < Phase)
				return true;

			return committedEvent.PreTagged > previous;
		}

		public override CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent) {
			if (committedEvent.PreTagged == null)
				throw new ArgumentException("committedEvent.PreTagged == null", "committedEvent");

			if (previous.Phase != Phase)
				throw new ArgumentException(
					string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

			return committedEvent.PreTagged;
		}

		public override CheckpointTag MakeCheckpointTag(CheckpointTag previous,
			ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof) {
			if (partitionEof.PreTagged == null)
				throw new ArgumentException("committedEvent.PreTagged == null", "committedEvent");

			if (previous.Phase != Phase)
				throw new ArgumentException(
					string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

			return partitionEof.PreTagged;
		}

		public override CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionDeleted partitionDeleted) {
			if (partitionDeleted.PreTagged == null)
				throw new ArgumentException("committedEvent.PreTagged == null", "committedEvent");

			if (previous.Phase != Phase)
				throw new ArgumentException(
					string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

			return partitionDeleted.PreTagged;
		}

		public override CheckpointTag MakeZeroCheckpointTag() {
			return _zeroCheckpointTag;
		}

		public override bool IsCompatible(CheckpointTag checkpointTag) {
			return true; //TODO: implement properly
		}

		public override CheckpointTag AdjustTag(CheckpointTag tag) {
			if (tag.Phase < Phase)
				return tag;
			if (tag.Phase > Phase)
				throw new ArgumentException(
					string.Format("Invalid checkpoint tag phase.  Expected less or equal to: {0} Was: {1}", Phase,
						tag.Phase), "tag");

			return tag;
		}
	}
}
