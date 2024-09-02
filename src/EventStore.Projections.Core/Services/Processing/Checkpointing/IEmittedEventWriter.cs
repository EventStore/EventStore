using System;
using EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

namespace EventStore.Projections.Core.Services.Processing.Checkpointing;

public interface IEmittedEventWriter {
	void EventsEmitted(EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId);
}
