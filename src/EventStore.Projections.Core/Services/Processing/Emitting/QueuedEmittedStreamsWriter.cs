// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Security.Claims;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing.Emitting;

public class QueuedEmittedStreamsWriter : IEmittedStreamsWriter {
	private IODispatcher _ioDispatcher;
	private Guid _writeQueueId;

	public QueuedEmittedStreamsWriter(IODispatcher ioDispatcher, Guid writeQueueId) {
		_ioDispatcher = ioDispatcher;
		_writeQueueId = writeQueueId;
	}

	public void WriteEvents(string streamId, long expectedVersion, Event[] events, ClaimsPrincipal writeAs,
		Action<ClientMessage.WriteEventsCompleted> complete) {
		_ioDispatcher.QueueWriteEvents(_writeQueueId, streamId, expectedVersion, events, writeAs, complete);
	}
}
