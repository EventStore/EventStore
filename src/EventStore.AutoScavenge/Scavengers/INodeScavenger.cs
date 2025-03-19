// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.AutoScavenge.Scavengers;

public interface INodeScavenger {
	/// <returns>Returns a scavenge id if it was successful, null otherwise</returns>
	Task<Guid?> TryStartScavengeAsync(string host, int port, CancellationToken token);

	Task<ScavengeStatus> TryGetScavengeAsync(string host, int port, Guid scavengeId, CancellationToken token);

	Task<bool> TryPauseScavengeAsync(string host, int port, Guid? scavengeId, CancellationToken token);
}
