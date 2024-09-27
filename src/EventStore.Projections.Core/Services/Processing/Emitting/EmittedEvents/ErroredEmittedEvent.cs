// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Projections.Core.Services.Processing.Emitting.EmittedEvents;

sealed class ErroredEmittedEvent : IValidatedEmittedEvent {
	public Exception Exception { get; private set; }

	public ErroredEmittedEvent(InvalidEmittedEventSequenceException exception) {
		Exception = exception;
	}
}
