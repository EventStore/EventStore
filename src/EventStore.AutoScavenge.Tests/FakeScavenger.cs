// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.AutoScavenge.Scavengers;

namespace EventStore.AutoScavenge.Tests;

public class FakeScavenger : INodeScavenger {
	public Dictionary<Guid, ScavengingNode> Scavenges { get; } = new();

	public bool Enabled { get; set; } = true;
	public bool Pausable { get; set; } = true;

	public Task<Guid?> TryStartScavengeAsync(string host, int port, CancellationToken token) {
		if (!Enabled)
			return Task.FromResult<Guid?>(null);

		var scavengeId = Guid.NewGuid();

		Scavenges.Add(scavengeId, new ScavengingNode {
			ScavengeId = scavengeId,
			Host = host,
			Port = port,
		});

		return Task.FromResult((Guid?)scavengeId);
	}

	public Task<ScavengeStatus> TryGetScavengeAsync(string host, int port, Guid scavengeId, CancellationToken token) {
		return Task.FromResult(Scavenges[scavengeId].Status);
	}

	public Task<bool> TryPauseScavengeAsync(string host, int port, Guid? scavengeId, CancellationToken token) {
		if (!Pausable)
			return Task.FromResult(false);

		if (scavengeId is null) {
			if (Scavenges.Count == 1) {
				scavengeId = Scavenges.Single().Key;
			} else {
				return Task.FromResult(true);
			}
		}

		Scavenges[scavengeId.Value].Status = ScavengeStatus.Stopped;
		return Task.FromResult(true);
	}

	public void SetScavengeStatus(Guid scavengeId, ScavengeStatus status) {
		Scavenges[scavengeId].Status = status;
	}

	public void RegisterScavengeFor(Guid scavengeId, ClusterMember member, ScavengeStatus status) {
		Scavenges.Add(scavengeId, new ScavengingNode {
			ScavengeId = scavengeId,
			Host = member.InternalHttpEndPointIp,
			Port = member.InternalHttpEndPointPort,
			Status = status,
		});
	}

	public class ScavengingNode {
		public Guid ScavengeId { get; init; } = Guid.Empty;
		public string Host { get; init; } = string.Empty;
		public int Port { get; init; }
		public ScavengeStatus Status { get; set; }
	}
}
