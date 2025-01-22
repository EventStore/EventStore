// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using static EventStore.Core.Messages.ClientMessage;

namespace EventStore.Core.Services.Storage.InMemory;

public class GossipListenerService(Guid nodeId, IPublisher publisher, InMemoryLog memLog) :
	IHandle<GossipMessage.GossipUpdated> {
	public SingleEventVirtualStream Stream { get; } = new(publisher, memLog, SystemStreams.GossipStream);
	public const string EventType = "$GossipUpdated";

	private readonly JsonSerializerOptions _options = new() { Converters = { new JsonStringEnumConverter() } };

	public void Handle(GossipMessage.GossipUpdated message) {
		// SystemStreams.GossipStream is a system stream so only readable by admins
		// we use ClientMemberInfo because plugins will consume this stream and
		// it is less likely to change than the internal gossip.
		var payload = new {
			NodeId = nodeId,
			Members = message.ClusterInfo.Members.Select(static x =>
				new Cluster.ClientClusterInfo.ClientMemberInfo(x)),
		};

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
