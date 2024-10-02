// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Projections.Core.Services.Processing.Checkpointing;

namespace EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

sealed class EmittedEventResolutionNeeded : IValidatedEmittedEvent {
	public string StreamId { get; }
	public long Revision { get; }
	public Tuple<CheckpointTag, string, long> TopCommitted { get; }

	public EmittedEventResolutionNeeded(string streamId, long revision, Tuple<CheckpointTag, string, long> topCommitted) {
		StreamId = streamId;
		Revision = revision;
		TopCommitted = topCommitted;
	}
}
