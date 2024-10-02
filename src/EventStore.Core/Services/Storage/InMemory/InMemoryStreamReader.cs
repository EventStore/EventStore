// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.Storage.InMemory;

public class InMemoryStreamReader : IInMemoryStreamReader {
	private readonly Dictionary<string, IInMemoryStreamReader> _readers;

	public InMemoryStreamReader(Dictionary<string, IInMemoryStreamReader> readers) {
		_readers = readers;
	}

	public ClientMessage.ReadStreamEventsForwardCompleted ReadForwards(ClientMessage.ReadStreamEventsForward msg) {
		if (_readers.TryGetValue(msg.EventStreamId, out var reader))
			return reader.ReadForwards(msg);

		return new ClientMessage.ReadStreamEventsForwardCompleted(
			msg.CorrelationId,
			msg.EventStreamId,
			msg.FromEventNumber,
			msg.MaxCount,
			ReadStreamResult.NoStream,
			Array.Empty<ResolvedEvent>(),
			StreamMetadata.Empty,
			isCachePublic: false,
			error: string.Empty,
			nextEventNumber: -1,
			lastEventNumber: ExpectedVersion.NoStream,
			isEndOfStream: true,
			tfLastCommitPosition: -1);
	}

	public ClientMessage.ReadStreamEventsBackwardCompleted ReadBackwards(ClientMessage.ReadStreamEventsBackward msg) {
		if (_readers.TryGetValue(msg.EventStreamId, out var reader))
			return reader.ReadBackwards(msg);

		return new ClientMessage.ReadStreamEventsBackwardCompleted(
			msg.CorrelationId,
			msg.EventStreamId,
			msg.FromEventNumber,
			msg.MaxCount,
			ReadStreamResult.NoStream,
			Array.Empty<ResolvedEvent>(),
			streamMetadata: StreamMetadata.Empty,
			isCachePublic: false,
			error: string.Empty,
			nextEventNumber: -1,
			lastEventNumber: ExpectedVersion.NoStream,
			isEndOfStream: true,
			tfLastCommitPosition: -1);
	}
}