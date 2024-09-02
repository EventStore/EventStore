using System;

namespace EventStore.Projections.Core.Services.Processing.Emitting;

class InvalidEmittedEventSequenceException : Exception {
	public InvalidEmittedEventSequenceException(string message)
		: base(message) {
	}
}
