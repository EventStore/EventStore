using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.DataStructures;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Storage.EpochManager {
	public class EpochManager : IEpochManager {
		private static readonly ILogger Log = Serilog.Log.ForContext<EpochManager>();
		private readonly IPublisher _bus;

		public readonly int CachedEpochCount;

		public int LastEpochNumber {
			get { return _lastEpochNumber; }
		}

		private readonly ICheckpoint _epochCheckpoint;
		private readonly ICheckpoint _termCheckpoint;
		private readonly ObjectPool<ITransactionFileReader> _readers;
		private readonly ITransactionFileWriter _writer;
		private readonly Guid _instanceId;

		private readonly object _locker = new object();
		private readonly SortedDictionary<int, EpochRecord> _epochs = new SortedDictionary<int, EpochRecord>();
		private volatile int _lastEpochNumber = -1;
		private long _lastEpochPosition = -1;
		private int _minCachedEpochNumber = -1;

		public EpochManager(IPublisher bus,
			int cachedEpochCount,
			ICheckpoint epochCheckpoint,
			ICheckpoint termCheckpoint,
			ITransactionFileWriter writer,
			int initialReaderCount,
			int maxReaderCount,
			Func<ITransactionFileReader> readerFactory,
			Guid instanceId) {
			Ensure.NotNull(bus, "bus");
			Ensure.Nonnegative(cachedEpochCount, "cachedEpochCount");
			Ensure.NotNull(epochCheckpoint, "epochCheckpoint");
			Ensure.NotNull(termCheckpoint, "termCheckpoint");
			Ensure.NotNull(writer, "chunkWriter");
			Ensure.Nonnegative(initialReaderCount, "initialReaderCount");
			Ensure.Positive(maxReaderCount, "maxReaderCount");
			if (initialReaderCount > maxReaderCount)
				throw new ArgumentOutOfRangeException("initialReaderCount",
					"initialReaderCount is greater than maxReaderCount.");
			Ensure.NotNull(readerFactory, "readerFactory");

			_bus = bus;
			CachedEpochCount = cachedEpochCount;
			_epochCheckpoint = epochCheckpoint;
			_termCheckpoint = termCheckpoint;
			_readers = new ObjectPool<ITransactionFileReader>("EpochManager readers pool", initialReaderCount,
				maxReaderCount, readerFactory);
			_writer = writer;
			_instanceId = instanceId;
		}

		public void Init() {
			ReadEpochs(CachedEpochCount);
			InitializeTermCheckpoint();
		}

		private void InitializeTermCheckpoint() {
			if (_termCheckpoint.ReadNonFlushed() != -1)
				return;

			Log.Verbose("Initializing the term checkpoint to the value of the last epoch number: {lastEpochNumber}", _lastEpochNumber);
			_termCheckpoint.Write(_lastEpochNumber);
			_termCheckpoint.Flush();
		}

		public EpochRecord GetLastEpoch() {
			lock (_locker) {
				return _lastEpochNumber < 0 ? null : GetEpoch(_lastEpochNumber, throwIfNotFound: true);
			}
		}

		private void ReadEpochs(int maxEpochCount) {
			lock (_locker) {
				var reader = _readers.Get();
				try {
					long epochPos = _epochCheckpoint.Read();
					if (epochPos < 0) // we probably have lost/uninitialized epoch checkpoint
					{
						reader.Reposition(_writer.Checkpoint.Read());

						SeqReadResult result;
						while ((result = reader.TryReadPrev()).Success) {
							var rec = result.LogRecord;
							if (rec.RecordType != LogRecordType.System ||
							    ((SystemLogRecord)rec).SystemRecordType != SystemRecordType.Epoch)
								continue;
							epochPos = rec.LogPosition;
							break;
						}
					}

					int cnt = 0;
					while (epochPos >= 0 && cnt < maxEpochCount) {
						var result = reader.TryReadAt(epochPos);
						if (!result.Success)
							throw new Exception(string.Format("Could not find Epoch record at LogPosition {0}.",
								epochPos));
						if (result.LogRecord.RecordType != LogRecordType.System)
							throw new Exception(string.Format("LogRecord is not SystemLogRecord: {0}.",
								result.LogRecord));

						var sysRec = (SystemLogRecord)result.LogRecord;
						if (sysRec.SystemRecordType != SystemRecordType.Epoch)
							throw new Exception(string.Format("SystemLogRecord is not of Epoch sub-type: {0}.",
								result.LogRecord));

						var epoch = sysRec.GetEpochRecord();
						_epochs[epoch.EpochNumber] = epoch;
						_lastEpochNumber = Math.Max(_lastEpochNumber, epoch.EpochNumber);
						_lastEpochPosition = Math.Max(_lastEpochPosition, epoch.EpochPosition);
						_minCachedEpochNumber = epoch.EpochNumber;

						epochPos = epoch.PrevEpochPosition;
						cnt += 1;
					}
				} finally {
					_readers.Return(reader);
				}
			}
		}

		public EpochRecord[] GetLastEpochs(int maxCount) {
			lock (_locker) {
				var res = new List<EpochRecord>();
				for (int epochNum = _lastEpochNumber, n = maxCount; epochNum >= 0 && n > 0; --epochNum, --n) {
					EpochRecord epoch;
					if (!_epochs.TryGetValue(epochNum, out epoch))
						break;
					res.Add(epoch);
				}

				return res.ToArray();
			}
		}
		public EpochRecord GetEpoch(int epochNumber, bool throwIfNotFound) {
			lock (_locker) {
				if (epochNumber < _minCachedEpochNumber) {
					if (!throwIfNotFound)
						return null;
					throw new ArgumentOutOfRangeException(
						"epochNumber",
						string.Format("EpochNumber requested should not be cached. Requested: {0}, min cached: {1}.",
							epochNumber,
							_minCachedEpochNumber));
				}

				EpochRecord epoch;
				if (!_epochs.TryGetValue(epochNumber, out epoch) && throwIfNotFound)
					throw new Exception(string.Format("Concurrency failure, epoch #{0} should not be null.",
						epochNumber));
				return epoch;
			}
		}

		public EpochRecord GetNextEpoch(int epochNumber, bool throwIfNotFound) {
			lock (_locker) {
				if (epochNumber < _minCachedEpochNumber) {
					if (!throwIfNotFound)
						return null;
					throw new ArgumentOutOfRangeException(
						"epochNumber",
						string.Format("EpochNumber requested is not cached. Requested: {0}, min cached: {1}.",
							epochNumber,
							_minCachedEpochNumber));
				}

				var keys = _epochs.Keys.ToArray();
				var epochIndex = Array.BinarySearch(keys, epochNumber);

				EpochRecord nextEpoch = null;

				if (epochIndex < 0 && throwIfNotFound) {
					throw new Exception($"Epoch record not found for epoch number: {epochNumber}");
				}

				if (0 <= epochIndex && epochIndex < keys.Length-1) {
					var nextEpochNumber = keys[epochIndex + 1];
					_epochs.TryGetValue(nextEpochNumber, out nextEpoch);
				}

				if (nextEpoch == null && throwIfNotFound) {
					throw new Exception($"Next Epoch record not found after epoch number: {epochNumber}");
				}

				return nextEpoch;
			}
		}

		public bool IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId) {
			Ensure.Nonnegative(epochPosition, "logPosition");
			Ensure.Nonnegative(epochNumber, "epochNumber");
			Ensure.NotEmptyGuid(epochId, "epochId");

			lock (_locker) {
				if (epochNumber > _lastEpochNumber)
					return false;
				if (epochNumber >= _minCachedEpochNumber) {
					var epoch = _epochs[epochNumber];
					return epoch.EpochId == epochId && epoch.EpochPosition == epochPosition;
				}
			}

			// epochNumber < _minCachedEpochNumber
			var reader = _readers.Get();
			try {
				var res = reader.TryReadAt(epochPosition);
				if (!res.Success || res.LogRecord.RecordType != LogRecordType.System)
					return false;
				var sysRec = (SystemLogRecord)res.LogRecord;
				if (sysRec.SystemRecordType != SystemRecordType.Epoch)
					return false;

				var epoch = sysRec.GetEpochRecord();
				return epoch.EpochNumber == epochNumber && epoch.EpochId == epochId;
			} finally {
				_readers.Return(reader);
			}
		}

		// This method should be called from single thread.
		public void WriteNewEpoch(int epochNumber) {
			// Now we write epoch record (with possible retry, if we are at the end of chunk) 
			// and update EpochManager's state, by adjusting cache of records, epoch count and un-caching 
			// excessive record, if present.
			// If we are writing the very first epoch, last position will be -1.
			if(epochNumber <= _lastEpochNumber) throw new ArgumentException("epochNumber <= _lastEpochNumber");
			var epoch = WriteEpochRecordWithRetry(epochNumber, Guid.NewGuid(), _lastEpochPosition, _instanceId);
			UpdateLastEpoch(epoch, flushWriter: true);
		}

		private EpochRecord WriteEpochRecordWithRetry(int epochNumber, Guid epochId, long lastEpochPosition, Guid instanceId) {
			long pos = _writer.Checkpoint.ReadNonFlushed();
			var epoch = new EpochRecord(pos, epochNumber, epochId, lastEpochPosition, DateTime.UtcNow, instanceId);
			var rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch,
				SystemRecordSerialization.Json, epoch.AsSerialized());

			if (!_writer.Write(rec, out pos)) {
				epoch = new EpochRecord(pos, epochNumber, epochId, lastEpochPosition, DateTime.UtcNow, instanceId);
				rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch,
					SystemRecordSerialization.Json, epoch.AsSerialized());
				if (!_writer.Write(rec, out pos))
					throw new Exception(string.Format("Second write try failed at {0}.", epoch.EpochPosition));
			}

			Log.Debug("=== Writing E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}). L={leaderId:B}.",
				epochNumber, epoch.EpochPosition, epochId, lastEpochPosition, epoch.LeaderInstanceId);

			_bus.Publish(new SystemMessage.EpochWritten(epoch));
			return epoch;
		}

		public void SetLastEpoch(EpochRecord epoch) {
			Ensure.NotNull(epoch, "epoch");

			lock (_locker) {
				if (epoch.EpochPosition > _lastEpochPosition) {
					UpdateLastEpoch(epoch, flushWriter: false);
					return;
				}
			}

			// Epoch record must have been already written, so we need to make sure it is where we expect it to be.
			// If this check fails, then there is something very wrong with epochs, data corruption is possible.
			if (!IsCorrectEpochAt(epoch.EpochPosition, epoch.EpochNumber, epoch.EpochId)) {
				throw new Exception(string.Format("Not found epoch at {0} with epoch number: {1} and epoch ID: {2}. "
				                                  + "SetLastEpoch FAILED! Data corruption risk!",
					epoch.EpochPosition,
					epoch.EpochNumber,
					epoch.EpochId));
			}
		}

		private void UpdateLastEpoch(EpochRecord epoch, bool flushWriter) {
			lock (_locker) {
				_epochs[epoch.EpochNumber] = epoch;
				_lastEpochNumber = epoch.EpochNumber;
				_lastEpochPosition = epoch.EpochPosition;

				var keys = _epochs.Keys.ToArray();
				for (var i = 0; i + CachedEpochCount < keys.Length && i + 1 < keys.Length ; i++) {
					_epochs.Remove(keys[i]);
					_minCachedEpochNumber = keys[i+1];
				}

				if (flushWriter)
					_writer.Flush();
				// Now update epoch checkpoint, so on restart we don't scan sequentially TF.
				_epochCheckpoint.Write(epoch.EpochPosition);
				_epochCheckpoint.Flush();

				Log.Debug(
					"=== Update Last Epoch E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}) L={leaderId:B}.",
					epoch.EpochNumber, epoch.EpochPosition, epoch.EpochId, epoch.PrevEpochPosition, epoch.LeaderInstanceId);
			}
		}

		public EpochRecord GetNextEpochWithAllEpochs(int epochNumber, bool throwIfNotFound) {
			ReadEpochs(int.MaxValue);
			return GetNextEpoch(epochNumber, throwIfNotFound);
		}
	}
}
