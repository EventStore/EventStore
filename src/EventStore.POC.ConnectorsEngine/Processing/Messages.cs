// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.POC.ConnectorsEngine.Infrastructure;

namespace EventStore.POC.ConnectorsEngine.Processing;

public static class Messages {

	public record GetConnectorList(Action<ConnectorState[]> Reply) : Message;

	public record GetActiveConnectorsList(Action<string[]> Reply) : Message;

	public record CaughtUp() : Message;

	public record GossipUpdated(Guid NodeId, Member[] Members) : Message;

	public record AllCheckpointed(
		ulong Round,
		ulong CommitPosition,
		ulong PreparePosition) : Message;
}

public record Member(Guid InstanceId, NodeState State, bool IsAlive);
