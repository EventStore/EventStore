using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.EpochManager {
	public interface IEpochManager {
		int LastEpochNumber { get; }

		void Init();

		EpochRecord GetLastEpoch();
		EpochRecord[] GetLastEpochs(int maxCount);
		EpochRecord GetEpoch(int epochNumber, bool throwIfNotFound);
		EpochRecord GetEpochWithAllEpochs(int epochNumber, bool throwIfNotFound);
		bool IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId);

		void WriteNewEpoch();
		void SetLastEpoch(EpochRecord epoch);
	}
}
