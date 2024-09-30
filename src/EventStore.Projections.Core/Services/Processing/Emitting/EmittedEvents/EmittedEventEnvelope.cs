// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents {
	public sealed class EmittedEventEnvelope {
		public readonly EmittedEvent Event;
		public readonly EmittedStream.WriterConfiguration.StreamMetadata StreamMetadata;

		public EmittedEventEnvelope(
			EmittedEvent @event, EmittedStream.WriterConfiguration.StreamMetadata streamMetadata = null) {
			Event = @event;
			StreamMetadata = streamMetadata;
		}
	}
}
