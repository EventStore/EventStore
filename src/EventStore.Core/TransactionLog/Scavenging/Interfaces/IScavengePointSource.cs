// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

public interface IScavengePointSource {
	// returns null when no scavenge point
	Task<ScavengePoint> GetLatestScavengePointOrDefaultAsync(CancellationToken cancellationToken);
	Task<ScavengePoint> AddScavengePointAsync(
		long expectedVersion,
		int threshold,
		CancellationToken cancellationToken);
}
