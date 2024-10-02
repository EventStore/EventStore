// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json;
using System.Text.Json.Serialization;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.Storage.InMemory;

// threading: we expect to handle one StateChangeMessage at a time, but Reads can happen concurrently
// with those handlings and with other reads.
public class NodeStateListenerService :
	IInMemoryStreamReader,
	IHandle<SystemMessage.StateChangeMessage> {

	private readonly SingleEventInMemoryStream _stream;

	public const string EventType = "$NodeStateChanged";

	private readonly JsonSerializerOptions _options = new() {
		Converters = {
			new JsonStringEnumConverter(),
		},
	};

	public NodeStateListenerService(IPublisher publisher, InMemoryLog memLog) {
		_stream = new(publisher, memLog, SystemStreams.NodeStateStream);
	}

	public void Handle(SystemMessage.StateChangeMessage message) {
		var payload = new { message.State };
		var data = JsonSerializer.SerializeToUtf8Bytes(payload, _options);
		_stream.Write(EventType, data);
	}

	public ClientMessage.ReadStreamEventsForwardCompleted ReadForwards(
		ClientMessage.ReadStreamEventsForward msg) => _stream.ReadForwards(msg);

	public ClientMessage.ReadStreamEventsBackwardCompleted ReadBackwards(
		ClientMessage.ReadStreamEventsBackward msg) => _stream.ReadBackwards(msg);
}
