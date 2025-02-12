// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.AutoScavenge;

public class EventStoreOptions {
	public bool Insecure { get; init; }
	public bool DiscoverViaDns { get; init; }
	public string? ClusterDns { get; init; }
	public int ClusterSize { get; init; }
}
