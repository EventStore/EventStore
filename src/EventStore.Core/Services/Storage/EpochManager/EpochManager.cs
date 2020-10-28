using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.DataStructures;
using EventStore.Core.Services.Replication;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.Storage.EpochManager {
	public class EpochManager : IEpochManager,
		IHandle<ReplicationTrackingMessage.ReplicatedTo>{
		private static readonly ILogger Log = Serilog.Log.ForContext<EpochManager>();
		private readonly IPublisher _bus;

		public readonly int CachedEpochCount;

		public int LastEpochNumber {
			get { return _lastEpochNumber; }
		}

		private readonly ICheckpoint _epochCheckpoint;
		private readonly ICheckpoint _epochStageCheckpoint;
		private readonly ICheckpoint _epochCommitCheckpoint;
		private readonly ICheckpoint _replicationCheckpoint;
		private readonly ObjectPool<ITransactionFileReader> _readers;
		private readonly ITransactionFileWriter _writer;
		private readonly Guid _instanceId;

		private readonly object _locker = new object();
		private readonly Dictionary<int, EpochRecord> _epochs = new Dictionary<int, EpochRecord>();
		private volatile int _lastEpochNumber = -1;
		private long _lastEpochPosition = -1;
		private int _minCachedEpochNumber = -1;
		private Queue<(EpochRecord epoch, long epochRecordPostPosition)> _nonReplicatedEpochs = new Queue<(EpochRecord epoch, long epochRecordPostPosition)>();
		private object _nonReplicatedEpochsLock = new object();

		public EpochManager(IPublisher bus,
			int cachedEpochCount,
			ICheckpoint epochCheckpoint,
			ICheckpoint epochStageCheckpoint,
			ICheckpoint epochCommitCheckpoint,
			ICheckpoint replicationCheckpoint,
			ITransactionFileWriter writer,
			int initialReaderCount,
			int maxReaderCount,
			Func<ITransactionFileReader> readerFactory,
			Guid instanceId) {
			Ensure.NotNull(bus, "bus");
			Ensure.Nonnegative(cachedEpochCount, "cachedEpochCount");
			Ensure.NotNull(epochCheckpoint, "epochCheckpoint");
			Ensure.NotNull(epochStageCheckpoint, "epochStageCheckpoint");
			Ensure.NotNull(epochCommitCheckpoint, "epochCommitCheckpoint");
			Ensure.NotNull(replicationCheckpoint, "replicationCheckpoint");
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
			_epochStageCheckpoint = epochStageCheckpoint;
			_epochCommitCheckpoint = epochCommitCheckpoint;
			_replicationCheckpoint = replicationCheckpoint;
			_readers = new ObjectPool<ITransactionFileReader>("EpochManager readers pool", initialReaderCount,
				maxReaderCount, readerFactory);
			_writer = writer;
			_instanceId = instanceId;
		}

		public void Init() {
			ReadReplicatedEpochs(CachedEpochCount);
			ReadNonReplicatedEpochs();
		}

		public EpochRecord GetLastEpoch() {
			lock (_locker) {
				return _lastEpochNumber < 0 ? null : GetEpoch(_lastEpochNumber, throwIfNotFound: true);
			}
		}

		private EpochRecord GetLastNonReplicatedEpoch() {
			lock (_nonReplicatedEpochsLock) {
				var lastEpoch = _nonReplicatedEpochs.LastOrDefault();
				if (!lastEpoch.Equals(default)) {
					return lastEpoch.epoch;
				}
			}

			return GetLastEpoch();
		}


		private void ReadReplicatedEpochs(int maxEpochCount) {
			lock (_locker) {
				var reader = _readers.Get();
				try {
					long epochPos = _epochCheckpoint.Read();
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

		private void ReadNonReplicatedEpochs() {
			var reader = _readers.Get();

			try {
				var epochStageChk = _epochStageCheckpoint.Read();
				var epochCommitChk = _epochCommitCheckpoint.Read();
				EpochRecord lastNonReplicatedEpoch = null;

				if (epochStageChk != -1) {
					try {
						lastNonReplicatedEpoch = ReadEpochRecordAtPosition(reader, epochStageChk).Epoch;
						Log.Verbose("Successfully read epoch record at epoch stage checkpoint ({epochStageCheckpoint}). Epoch record: {epoch}.", epochStageChk, lastNonReplicatedEpoch);
						if (epochCommitChk != epochStageChk) {
							Log.Verbose(
								"Setting the epoch commit checkpoint ({epochCommitCheckpoint}) to the value of the epoch stage checkpoint ({epochStageCheckpoint}).", epochCommitChk, epochStageChk);
							_epochCommitCheckpoint.Write(epochStageChk);
							_epochCommitCheckpoint.Flush();
						}
					} catch (Exception) {
						//it is possible that the epoch record was not yet written at this point
						//so we revert the stage checkpoint to the value of the commit checkpoint
						Log.Verbose("Failed to read epoch record at epoch stage checkpoint ({epochStageCheckpoint}). Reverting epoch stage checkpoint to the value of the epoch commit checkpoint ({epochCommitCheckpoint})",
							epochStageChk, epochCommitChk);
						_epochStageCheckpoint.Write(epochCommitChk);
						_epochStageCheckpoint.Flush();
					}
				}

				if (lastNonReplicatedEpoch == null && epochCommitChk != -1) {
					lastNonReplicatedEpoch = ReadEpochRecordAtPosition(reader, epochCommitChk).Epoch;
				}

				if (lastNonReplicatedEpoch == null) {
					return;
				}

				var nonReplicatedEpochs = new Stack<(EpochRecord epoch, long postPosition)>();
				var curEpochPos = lastNonReplicatedEpoch.EpochPosition;

				while(curEpochPos >=0 && curEpochPos > _lastEpochPosition) {
					var (epoch, postPosition) = ReadEpochRecordAtPosition(reader, curEpochPos);
					Log.Verbose("Successfully read non-replicated epoch record at {position}. Epoch record: {epoch}", curEpochPos, epoch);
					nonReplicatedEpochs.Push((epoch, postPosition));
					curEpochPos = epoch.PrevEpochPosition;
				}

				Log.Verbose("Loaded {count} non-replicated epoch records.", nonReplicatedEpochs.Count);
				while(nonReplicatedEpochs.TryPop(out var x)){
					EpochRecordChased(x.epoch, x.postPosition);
				}
			}
			finally {
				_readers.Return(reader);
			}
		}

		private (EpochRecord Epoch, long PostPosition) ReadEpochRecordAtPosition(ITransactionFileReader reader, long epochPos) {
			RecordReadResult result;
			if (!(result = reader.TryReadAt(epochPos)).Success)
				throw new Exception($"Unable to read epoch record at position: {epochPos}");

			var rec = result.LogRecord;
			if (rec.RecordType != LogRecordType.System)
				throw new Exception(string.Format("LogRecord is not SystemLogRecord: {0}.",
					result.LogRecord));

			var sysRec = (SystemLogRecord)rec;
			if (sysRec.SystemRecordType != SystemRecordType.Epoch)
				throw new Exception(string.Format("SystemLogRecord is not of Epoch sub-type: {0}.",
					result.LogRecord));

			var epoch = sysRec.GetEpochRecord();
			return (epoch, result.NextPosition);
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
		public void WriteNewEpoch() {
			// Now we write epoch record (with possible retry, if we are at the end of chunk) 
			// If we are writing the very first epoch, last position will be -1.
			// We will later update the last epoch after the epoch record is chased from the transaction log AND has been replicated to a quorum number of nodes
			var lastEpoch = GetLastNonReplicatedEpoch();
			var lastEpochNumber = lastEpoch?.EpochNumber ?? -1;
			var lastEpochPosition = lastEpoch?.EpochPosition ?? -1;

			long pos = _writer.Checkpoint.ReadNonFlushed();
			var epoch = new EpochRecord(pos, lastEpochNumber + 1, Guid.NewGuid(), lastEpochPosition, DateTime.UtcNow, _instanceId);
			WriteEpochRecordWithRetry(epoch);
		}

		public void WriteEpochRecordWithRetry(EpochRecord epoch) {
			var rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch,
				SystemRecordSerialization.Json, epoch.AsSerialized());
			var pos = epoch.EpochPosition;

			if (!_writer.Write(rec, out _)) { //write will fail if the chunk is full
				pos = _writer.Checkpoint.ReadNonFlushed();
				epoch = new EpochRecord(pos, epoch.EpochNumber, epoch.EpochId, epoch.PrevEpochPosition, DateTime.UtcNow, epoch.LeaderInstanceId);
				rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch,
					SystemRecordSerialization.Json, epoch.AsSerialized());
				if (!_writer.Write(rec, out _))
					throw new Exception(string.Format("Second write try failed at {0}.", epoch.EpochPosition));
			}

			_epochStageCheckpoint.Write(pos);
			_epochStageCheckpoint.Flush();

			//flush the writer to trigger the storage chaser and chase the epoch record
			_writer.Flush();

			_epochCommitCheckpoint.Write(pos);
			_epochCommitCheckpoint.Flush();

			Log.Debug("=== Writing E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}). L={leaderId:B}.",
				epoch.EpochNumber, epoch.EpochPosition, epoch.EpochId, epoch.PrevEpochPosition, epoch.LeaderInstanceId);

			_bus.Publish(new SystemMessage.EpochWritten(epoch));
		}

		public void EpochRecordChased(EpochRecord epoch, long epochRecordPostPosition) {
			Ensure.NotNull(epoch, "epoch");
			lock (_nonReplicatedEpochsLock) {
				Log.Verbose(
					"Enqueueing epoch record chased from transaction log: {epoch}. Epoch record post position: {epochRecordPostPosition}",
					epoch, epochRecordPostPosition);

				_nonReplicatedEpochs.Enqueue((epoch, epochRecordPostPosition));
			}
			ProcessNonReplicatedEpochRecords();
		}

		private void UpdateLastEpoch(EpochRecord epoch) {
			lock (_locker) {
				_epochs[epoch.EpochNumber] = epoch;
				_lastEpochNumber = epoch.EpochNumber;
				_lastEpochPosition = epoch.EpochPosition;
				_minCachedEpochNumber = Math.Max(_minCachedEpochNumber, epoch.EpochNumber - CachedEpochCount + 1);
				_epochs.Remove(_minCachedEpochNumber - 1);

				_epochCheckpoint.Write(epoch.EpochPosition);
				_epochCheckpoint.Flush();

				Log.Debug(
					"=== Update Last Epoch E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}) L={leaderId:B}.",
					epoch.EpochNumber, epoch.EpochPosition, epoch.EpochId, epoch.PrevEpochPosition, epoch.LeaderInstanceId);
			}
		}

		public EpochRecord GetEpochWithAllEpochs(int epochNumber, bool throwIfNotFound) {
			ReadReplicatedEpochs(int.MaxValue);
			return GetEpoch(epochNumber, throwIfNotFound);
		}

		public void Handle(ReplicationTrackingMessage.ReplicatedTo message) {
			ProcessNonReplicatedEpochRecords();
		}

		private void ProcessNonReplicatedEpochRecords() {
			var replicationCheckpoint = _replicationCheckpoint.Read();
			lock (_nonReplicatedEpochsLock) {
				if (_nonReplicatedEpochs.Count > 0) {
					Log.Verbose(
						"Processing {count} non-replicated epoch record(s). Replication checkpoint: {replicationCheckpoint}",
						_nonReplicatedEpochs.Count, replicationCheckpoint);
				}

				while (_nonReplicatedEpochs.Count > 0) {
					var head = _nonReplicatedEpochs.Peek();
					if (head.epochRecordPostPosition > replicationCheckpoint) {
						Log.Verbose("Epoch record: {epoch} has not yet been replicated to a quorum number of nodes. Epoch record post position: {epochRecordPostPosition} / Replication checkpoint: {replicationCheckpoint}", head.epoch, head.epochRecordPostPosition, replicationCheckpoint);
						break;
					}

					Log.Verbose("Epoch record: {epoch} has been replicated to a quorum number of nodes. Epoch record post position: {epochRecordPostPosition} / Replication checkpoint: {replicationCheckpoint}", head.epoch, head.epochRecordPostPosition, replicationCheckpoint);

					lock (_locker) {
						if (head.epoch.EpochPosition > _lastEpochPosition) {
							Log.Verbose("Updating last epoch to: {epoch}. Last Epoch number: {lastEpochNumber} / Last epoch position: {lastEpochPosition}", head.epoch, _lastEpochNumber, _lastEpochPosition);

							UpdateLastEpoch(head.epoch);
						}
					}
					_nonReplicatedEpochs.Dequeue();
				}
			}
		}
	}
}
