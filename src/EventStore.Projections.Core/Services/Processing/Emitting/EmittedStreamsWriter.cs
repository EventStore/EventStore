// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Claims;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Emitting;

public class EmittedStreamsWriter : IEmittedStreamsWriter {
	private IODispatcher _ioDispatcher;

	public EmittedStreamsWriter(IODispatcher ioDispatcher) {
		_ioDispatcher = ioDispatcher;
	}

	public void WriteEvents(string streamId, long expectedVersion, Event[] events, ClaimsPrincipal writeAs,
		Action<ClientMessage.WriteEventsCompleted> complete) {
		_ioDispatcher.WriteEvents(streamId, expectedVersion, events, writeAs, complete);
	}
}
