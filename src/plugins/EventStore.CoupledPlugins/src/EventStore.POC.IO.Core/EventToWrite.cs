// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.POC.IO.Core;

public class EventToWrite {
	public Guid EventId { get; init; }
	public string EventType { get; init; }
	public string ContentType { get; init; }
	public ReadOnlyMemory<byte> Data { get; init; }
	public ReadOnlyMemory<byte> Metadata { get; init; }

	public EventToWrite(
		Guid eventId,
		string eventType,
		string contentType,
		ReadOnlyMemory<byte> data,
		ReadOnlyMemory<byte> metadata) {

		EventId = eventId;
		EventType = eventType;
		ContentType = contentType;
		Data = data;
		Metadata = metadata;
	}
}
