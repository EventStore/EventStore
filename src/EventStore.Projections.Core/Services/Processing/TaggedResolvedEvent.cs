// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Services.Processing.Checkpointing;

namespace EventStore.Projections.Core.Services.Processing {
	public sealed class TaggedResolvedEvent {
		public readonly ResolvedEvent ResolvedEvent;
		public readonly CheckpointTag ReaderPosition;

		public TaggedResolvedEvent(ResolvedEvent resolvedEvent, CheckpointTag readerPosition) {
			ResolvedEvent = resolvedEvent;
			ReaderPosition = readerPosition;
		}
	}
}
