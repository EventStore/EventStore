// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing.Checkpointing;

namespace EventStore.Projections.Core.XUnit.Tests.TestHelpers;

// TODO: Flesh out this helper as more tests need it
public class FakePositionTagger : PositionTagger {
	public FakePositionTagger(int phase) : base(phase) {
	}

	public override bool IsMessageAfterCheckpointTag(CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent) {
		throw new NotImplementedException();
	}

	public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ReaderSubscriptionMessage.CommittedEventDistributed committedEvent) {
		throw new NotImplementedException();
	}

	public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionEof partitionEof) {
		throw new NotImplementedException();
	}

	public override CheckpointTag MakeCheckpointTag(CheckpointTag previous, ReaderSubscriptionMessage.EventReaderPartitionDeleted partitionDeleted) {
		throw new NotImplementedException();
	}

	public override CheckpointTag MakeZeroCheckpointTag() {
		return CheckpointTag.Empty;
	}

	public override bool IsCompatible(CheckpointTag checkpointTag) {
		throw new NotImplementedException();
	}

	public override CheckpointTag AdjustTag(CheckpointTag tag) {
		return tag;
	}
}

