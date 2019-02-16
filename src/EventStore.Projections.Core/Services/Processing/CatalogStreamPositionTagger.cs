using System;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing {
	public class CatalogStreamPositionTagger : PositionTagger {
		private readonly string _catalogStream;

		public CatalogStreamPositionTagger(int phase, string catalogStream)
			: base(phase) {
			_catalogStream = catalogStream;
		}

		public override bool IsMessageAfterCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent) {
			if (previous.Phase < Phase)
				return true;
			if (previous.Mode_ != CheckpointTag.Mode.ByStream)
				throw new ArgumentException("Mode.Stream expected", "previous");
			return committedEvent.Data.PositionStreamId == _catalogStream
			       && committedEvent.Data.PositionSequenceNumber > previous.CatalogPosition;
		}

		public override CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent) {
			if (previous.Phase != Phase)
				throw new ArgumentException(
					string.Format("Invalid checkpoint tag phase.  Expected: {0} Was: {1}", Phase, previous.Phase));

			if (committedEvent.Data.PositionStreamId != _catalogStream)
				throw new InvalidOperationException(
					string.Format(
						"Invalid catalog stream '{0}'.  Expected catalog stream is '{1}'",
						committedEvent.Data.EventStreamId, _catalogStream));

			return CheckpointTag.FromByStreamPosition(
				previous.Phase, "", committedEvent.Data.PositionSequenceNumber, null,
				-1, previous.CommitPosition.GetValueOrDefault());
		}

		public override CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof) {
			throw new NotImplementedException();
		}

		public override CheckpointTag MakeCheckpointTag(CheckpointTag previous,
			ReaderSubscriptionMessage.EventReaderPartitionDeleted partitionDeleted) {
			throw new NotSupportedException();
		}

		public override CheckpointTag MakeZeroCheckpointTag() {
			return CheckpointTag.FromByStreamPosition(
				Phase, "", -1, null,
				-1, int.MinValue);
		}

		public override bool IsCompatible(CheckpointTag checkpointTag) {
			return checkpointTag.Mode_ == CheckpointTag.Mode.ByStream && checkpointTag.CatalogStream == "";
		}

		public override CheckpointTag AdjustTag(CheckpointTag tag) {
			if (tag.Phase < Phase)
				return tag;
			if (tag.Phase > Phase)
				throw new ArgumentException(
					string.Format("Invalid checkpoint tag phase.  Expected less or equal to: {0} Was: {1}", Phase,
						tag.Phase), "tag");

			if (tag.Mode_ == CheckpointTag.Mode.ByStream)
				return tag;
			switch (tag.Mode_) {
				default:
					throw new NotSupportedException("Conversion is not supported");
			}
		}
	}
}
