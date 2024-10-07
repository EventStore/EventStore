// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.Messaging;

// The name of this enum and its members are used for metrics
public enum CoreMessage {
	None,
	Authentication,
	Awake,
	Client,
	ClusterClient,
	Election,
	Gossip,
	Grpc,
	Http,
	IODispatcher,
	LeaderDiscovery,
	Monitoring,
	Misc,
	Redaction,
	Replication,
	ReplicationTracking,
	Storage,
	Subscription,
	System,
	Tcp,
	Telemetry,
	Timer,
	UserManagement
}
