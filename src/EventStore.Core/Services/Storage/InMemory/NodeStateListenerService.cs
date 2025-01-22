// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using static EventStore.Core.Messages.ClientMessage;

namespace EventStore.Core.Services.Storage.InMemory;

// threading: we expect to handle one StateChangeMessage at a time, but Reads can happen concurrently
// with those handlings and with other reads.
public class NodeStateListenerService(IPublisher publisher, InMemoryLog memLog) : IHandle<SystemMessage.StateChangeMessage> {
	public SingleEventVirtualStream Stream { get; } = new(publisher, memLog, SystemStreams.NodeStateStream);

	public const string EventType = "$NodeStateChanged";

	private readonly JsonSerializerOptions _options = new() { Converters = { new JsonStringEnumConverter() } };

	public void Handle(SystemMessage.StateChangeMessage message) {
		var payload = new { message.State };
		var data = JsonSerializer.SerializeToUtf8Bytes(payload, _options);
		Stream.Write(EventType, data);
	}

	public ValueTask<ReadStreamEventsForwardCompleted> ReadForwards(ReadStreamEventsForward msg, CancellationToken token)
		=> Stream.ReadForwards(msg, token);

	public ValueTask<ReadStreamEventsBackwardCompleted> ReadBackwards(ReadStreamEventsBackward msg, CancellationToken token)
		=> Stream.ReadBackwards(msg, token);

	public ValueTask<long> GetLastEventNumber(string streamId) => Stream.GetLastEventNumber(streamId);

	public bool OwnStream(string streamId) => Stream.OwnStream(streamId);
}
