// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Projections.Core.Services.Processing.Checkpointing;

namespace EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

sealed class ValidEmittedEvent : IValidatedEmittedEvent {
	public CheckpointTag Checkpoint { get; private set; }
	public string EventType { get; private set; }
	public long Revision { get; private set; }

	public ValidEmittedEvent(CheckpointTag checkpoint, string eventType, long revision) {
		Checkpoint = checkpoint;
		EventType = eventType;
		Revision = revision;
	}
}
