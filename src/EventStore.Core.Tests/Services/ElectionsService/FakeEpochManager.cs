using System;
using System.Collections.Generic;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Services.ElectionsService {
	internal class FakeEpochManager : IEpochManager {
		public int LastEpochNumber {
			get { return -1; }
		}

		public void Init() {
		}

		public EpochRecord GetLastEpoch() {
			return null;
		}

		public EpochRecord[] GetLastEpochs(int maxCount) {
			throw new NotImplementedException();
		}

		public EpochRecord GetEpoch(int epochNumber, bool throwIfNotFound) {
			throw new NotImplementedException();
		}

		public EpochRecord GetEpochWithAllEpochs(int epochNumber, bool throwIfNotFound) {
			throw new NotImplementedException();
		}

		public bool IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId) {
			throw new NotImplementedException();
		}

		public void WriteNewEpoch() {
			throw new NotImplementedException();
		}

		public void SetLastEpoch(EpochRecord epoch) {
			throw new NotImplementedException();
		}

		public IEnumerable<EpochRecord> GetCachedEpochs() {
			throw new NotImplementedException();
		}
	}
}
