// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Checkpointing {
	public abstract class PositionTagger {
		public readonly int Phase;

		public PositionTagger(int phase) {
			Phase = phase;
		}

		public abstract bool IsMessageAfterCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent);

		public abstract CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent);

		public abstract CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof);

		public abstract CheckpointTag MakeCheckpointTag(
			CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionDeleted partitionDeleted);

		public abstract CheckpointTag MakeZeroCheckpointTag();

		public abstract bool IsCompatible(CheckpointTag checkpointTag);

		public abstract CheckpointTag AdjustTag(CheckpointTag tag);
	}
}
