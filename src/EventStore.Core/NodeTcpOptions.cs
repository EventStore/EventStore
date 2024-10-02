// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core;

// Still needed for gossip and stats.
public class NodeTcpOptions {
	public int NodeTcpPort { get; init; } = 1113;
	public bool EnableExternalTcp { get; init; }
	public int? NodeTcpPortAdvertiseAs { get; init; }
}
