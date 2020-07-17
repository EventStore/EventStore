using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Services.ElectionsService {
	internal class FakeEpochManager : IEpochManager {

		public int LastEpochNumber {
			get {
				lock (_epochs) {
					return _epochs.Any() ? _epochs.Last().EpochNumber : -1;
				}
			}
		}

		private readonly List<EpochRecord> _epochs = new List<EpochRecord>();
		public void Init() {
		}

		public EpochRecord GetLastEpoch() {
			lock (_epochs) {
				return _epochs.LastOrDefault();
			}
		}

		public EpochRecord[] GetLastEpochs(int maxCount) {
			lock (_epochs) {
				if (maxCount >= _epochs.Count) {
					return _epochs.ToArray();
				} else {
					return _epochs.Skip(_epochs.Count - maxCount).ToArray();
				}
			}
		}

		public EpochRecord GetEpoch(int epochNumber, bool throwIfNotFound) {
			lock (_epochs) {
				var epoch = _epochs.FirstOrDefault(e => e.EpochNumber == epochNumber);
				if (throwIfNotFound && epoch == null)
					throw new ArgumentOutOfRangeException(nameof(epochNumber), "Epoch not Found");
				return epoch;
			}
		}

		public EpochRecord GetEpochWithAllEpochs(int epochNumber, bool throwIfNotFound) {
			lock (_epochs) {
				return GetEpoch(epochNumber, throwIfNotFound);
			}
		}

		public bool IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId) {
			throw new NotImplementedException();
		}

		public void WriteNewEpoch(Action<EpochRecord> onEpochWritten) {
			throw new NotImplementedException();
		}

		public void SetLastEpoch(EpochRecord epoch) {
			lock (_epochs) {
				_epochs.Add(epoch);
			}
		}

		public IEnumerable<EpochRecord> GetCachedEpochs() {
			lock (_epochs) {
				return _epochs.AsReadOnly();
			}
		}
	}
}
