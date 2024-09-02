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
