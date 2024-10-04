// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.EpochManager;

public interface IEpochManager {
	int LastEpochNumber { get; }

	ValueTask Init(CancellationToken token);
	EpochRecord GetLastEpoch();
	ValueTask<IReadOnlyList<EpochRecord>> GetLastEpochs(int maxCount, CancellationToken token);
	ValueTask<EpochRecord> GetEpochAfter(int epochNumber, bool throwIfNotFound, CancellationToken token);
	ValueTask<bool> IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId, CancellationToken token);
	ValueTask WriteNewEpoch(int epochNumber, CancellationToken token);
	ValueTask CacheEpoch(EpochRecord epoch, CancellationToken token);
	ValueTask<EpochRecord> TryTruncateBefore(long position, CancellationToken token);
}
