// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Services.Replication;
using EventStore.Core.Services.Storage;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Tests.Services.Replication.LogReplication;

internal record ReplicaInfo<TStreamId> {
	public TFChunkDb Db { get; init; }
	public IPublisher Publisher { get; init; }
	public ReplicaService ReplicaService { get; init; }
	public IEpochManager EpochManager { get; init; }
	public StorageWriterService<TStreamId> Writer { get; init; }
	public Func<int> GetNumWriterFlushes { get; init; }
	public ReplicationInterceptor ReplicationInterceptor { get; init; }
	public AutoResetEvent ConnectionEstablished { get; init; }
	public Func<long> GetReplicationPosition { get; init; }
	public Action ResetSubscription { get; init; }
}
