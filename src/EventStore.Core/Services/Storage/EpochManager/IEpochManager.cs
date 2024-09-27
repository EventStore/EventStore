// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.EpochManager {
	public interface IEpochManager {
		int LastEpochNumber { get; }

		void Init();

		EpochRecord GetLastEpoch();
		EpochRecord[] GetLastEpochs(int maxCount);
		EpochRecord GetEpochAfter(int epochNumber, bool throwIfNotFound);
		bool IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId);
		void WriteNewEpoch(int epochNumber);
		void CacheEpoch(EpochRecord epoch);
		bool TryTruncateBefore(long position, out EpochRecord epoch);
	}
}
