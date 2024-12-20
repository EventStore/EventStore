// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.TransactionLog.Scavenging.Interfaces;

public interface IChunkMergerBackend {
	ValueTask MergeChunks(
		ITFChunkScavengerLog scavengerLogger,
		Throttle throttle,
		CancellationToken cancellationToken);
}
