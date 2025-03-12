// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.AutoScavenge.Domain;

namespace EventStore.AutoScavenge;

public class ClusterMember {
	public Guid InstanceId { get; set; } = default;
	public string State { get; set; } = string.Empty;
	public bool IsAlive { get; set; }
	public string InternalHttpEndPointIp { get; set; } = string.Empty;
	public int InternalHttpEndPointPort { get; set; }
	public long WriterCheckpoint { get; set; }
	public bool IsReadOnlyReplica { get; set; }
}

public static class ClusterMemberExtensions {
	public static NodeId ToNodeId(this ClusterMember m) =>
		new(m.InternalHttpEndPointIp, m.InternalHttpEndPointPort);
}
