using System;

namespace EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

sealed class ErroredEmittedEvent : IValidatedEmittedEvent {
	public Exception Exception { get; private set; }

	public ErroredEmittedEvent(InvalidEmittedEventSequenceException exception) {
		Exception = exception;
	}
}
