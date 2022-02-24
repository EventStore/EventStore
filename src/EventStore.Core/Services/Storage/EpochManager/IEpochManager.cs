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
