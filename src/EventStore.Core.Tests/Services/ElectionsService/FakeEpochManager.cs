using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNext.Threading;
using EventStore.Core.Services.Storage.EpochManager;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.Services.ElectionsService;

internal class FakeEpochManager : IEpochManager {

	public int LastEpochNumber {
		get {
			lock (_epochs) {
				return _epochs.Any() ? _epochs.Last().EpochNumber : -1;
			}
		}
	}

	private readonly AsyncExclusiveLock _lock = new();
	private readonly List<EpochRecord> _epochs = new();
	public ValueTask Init(CancellationToken token) => ValueTask.CompletedTask;

	public EpochRecord GetLastEpoch() {
		var task = _lock.AcquireAsync().AsTask();
		task.Wait();
		try {
			return _epochs.LastOrDefault();
		} finally {
			_lock.Release();
			task.Dispose();
		}
	}

	public async ValueTask<IReadOnlyList<EpochRecord>> GetLastEpochs(int maxCount, CancellationToken token) {
		await _lock.AcquireAsync(token);
		try {
			if (maxCount >= _epochs.Count) {
				return _epochs.ToArray();
			} else {
				return _epochs.Skip(_epochs.Count - maxCount).ToArray();
			}
		} finally {
			_lock.Release();
		}
	}

	public async ValueTask<EpochRecord> GetEpochAfter(int epochNumber, bool throwIfNotFound, CancellationToken token) {
		await _lock.AcquireAsync(token);
		try {
			var epoch = _epochs.FirstOrDefault(e => e.EpochNumber == epochNumber);
			if (epoch != null) {
				var index = _epochs.IndexOf(epoch);
				epoch = null;
				if (index + 1 < _epochs.Count) {
					epoch = _epochs[index + 1];
				}
			}

			if (throwIfNotFound && epoch == null)
				throw new ArgumentOutOfRangeException(nameof(epochNumber), "Epoch not Found");
			return epoch;
		} finally {
			_lock.Release();
		}
	}

	public async ValueTask<bool> IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId, CancellationToken token) {
		await _lock.AcquireAsync(token);
		try {
			var epoch = _epochs.FirstOrDefault(e => e.EpochNumber == epochNumber);
			if (epoch == null)
				return false;

			return epoch.EpochPosition == epochPosition &&
			       epoch.EpochId == epochId;
		} finally {
			_lock.Release();
		}
	}

	public ValueTask WriteNewEpoch(int epochNumber, CancellationToken token)
		=> ValueTask.FromException(new NotImplementedException());

	public async ValueTask CacheEpoch(EpochRecord epoch, CancellationToken token) {
		await _lock.AcquireAsync(token);
		try {
			_epochs.Add(epoch);
		} finally {
			_lock.Release();
		}
	}

	public ValueTask<EpochRecord> TryTruncateBefore(long position, CancellationToken token)
		=> ValueTask.FromException<EpochRecord>(new NotImplementedException());
}
