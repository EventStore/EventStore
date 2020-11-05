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

		public EpochRecord GetNextEpoch(int epochNumber, bool throwIfNotFound) {
			lock (_epochs) {
				var epochIndex = _epochs.FindIndex(e => e.EpochNumber == epochNumber);

				if (throwIfNotFound && epochIndex < 0) {
					throw new ArgumentOutOfRangeException(nameof(epochNumber), "Epoch not Found");
				}

				EpochRecord nextEpoch = null;
				if (0 <= epochIndex && epochIndex < _epochs.Count - 1) {
					nextEpoch = _epochs[epochIndex + 1];
				}

				if (throwIfNotFound && nextEpoch == null) {
					throw new Exception($"Next Epoch not found after epoch number: {epochNumber}");
				}

				return nextEpoch;
			}
		}

		public EpochRecord GetNextEpochWithAllEpochs(int epochNumber, bool throwIfNotFound) {
			lock (_epochs) {
				return GetNextEpoch(epochNumber, throwIfNotFound);
			}
		}

		public bool IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId) {
			throw new NotImplementedException();
		}

		public void WriteNewEpoch(int epochNumber) {
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
