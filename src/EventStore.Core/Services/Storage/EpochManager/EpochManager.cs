using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.DataStructures;
using EventStore.Core.LogAbstraction;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using ILogger = Serilog.ILogger;
using EventStore.LogCommon;

namespace EventStore.Core.Services.Storage.EpochManager {
	public class EpochManager : IEpochManager {
		private static readonly ILogger Log = Serilog.Log.ForContext<EpochManager>();
		private readonly IPublisher _bus;

		private readonly ICheckpoint _checkpoint;
		private readonly ObjectPool<ITransactionFileReader> _readers;
		private readonly ITransactionFileWriter _writer;
		private readonly IRecordFactory _recordFactory;
		private readonly Guid _instanceId;

		private readonly object _locker = new object();
		private readonly int _cacheSize;
		private readonly LinkedList<EpochRecord> _epochs = new LinkedList<EpochRecord>();

		private LinkedListNode<EpochRecord> _firstCachedEpoch;
		private LinkedListNode<EpochRecord> _lastCachedEpoch;
		public EpochRecord GetLastEpoch() => _lastCachedEpoch?.Value;
		public int LastEpochNumber => _lastCachedEpoch?.Value.EpochNumber ?? -1;


		public EpochManager(IPublisher bus,
			int cachedEpochCount,
			ICheckpoint checkpoint,
			ITransactionFileWriter writer,
			int initialReaderCount,
			int maxReaderCount,
			Func<ITransactionFileReader> readerFactory,
			IRecordFactory recordFactory,
			Guid instanceId) {
			Ensure.NotNull(bus, "bus");
			Ensure.Nonnegative(cachedEpochCount, "cachedEpochCount");
			Ensure.NotNull(checkpoint, "checkpoint");
			Ensure.NotNull(writer, "chunkWriter");
			Ensure.Nonnegative(initialReaderCount, "initialReaderCount");
			Ensure.Positive(maxReaderCount, "maxReaderCount");
			if (initialReaderCount > maxReaderCount)
				throw new ArgumentOutOfRangeException(nameof(initialReaderCount),
					"initialReaderCount is greater than maxReaderCount.");
			Ensure.NotNull(readerFactory, "readerFactory");

			_bus = bus;
			_cacheSize = cachedEpochCount;
			_checkpoint = checkpoint;
			_readers = new ObjectPool<ITransactionFileReader>("EpochManager readers pool", initialReaderCount,
				maxReaderCount, readerFactory);
			_writer = writer;
			_recordFactory = recordFactory;
			_instanceId = instanceId;
		}

		public void Init() {
			ReadEpochs(_cacheSize);
		}

		private void ReadEpochs(int maxEpochCount) {
			lock (_locker) {
				var reader = _readers.Get();
				try {

					long epochPos = _checkpoint.Read();
					if (epochPos < 0
					) // we probably have lost/uninitialized epoch checkpoint scan back to find the most recent epoch in the log
					{
						reader.Reposition(_writer.Checkpoint.Read());

						SeqReadResult result;
						while ((result = reader.TryReadPrev()).Success) {
							var rec = result.LogRecord;
							if (rec.RecordType != LogRecordType.System ||
								((ISystemLogRecord)rec).SystemRecordType != SystemRecordType.Epoch)
								continue;
							epochPos = rec.LogPosition;
							break;
						}
					}

					//read back down the chain of epochs in the log until the cache is full
					int cnt = 0;
					while (epochPos >= 0 && cnt < maxEpochCount) {
						var epoch = ReadEpochAt(reader, epochPos);
						_epochs.AddFirst(epoch);
						if(epoch.EpochPosition == 0){ break;}
						epochPos = epoch.PrevEpochPosition;
						cnt += 1;
					}

					_lastCachedEpoch = _epochs.Last;
					_firstCachedEpoch = _epochs.First;
				} finally {
					_readers.Return(reader);
				}
			}
		}
		private EpochRecord ReadEpochAt(ITransactionFileReader reader, long epochPos) {
			var result = reader.TryReadAt(epochPos);
			if (!result.Success)
				throw new Exception($"Could not find Epoch record at LogPosition {epochPos}.");
			if (result.LogRecord.RecordType != LogRecordType.System)
				throw new Exception($"LogRecord is not SystemLogRecord: {result.LogRecord}.");

			var sysRec = (ISystemLogRecord)result.LogRecord;
			if (sysRec.SystemRecordType != SystemRecordType.Epoch)
				throw new Exception($"SystemLogRecord is not of Epoch sub-type: {result.LogRecord}.");

			return sysRec.GetEpochRecord();
		}
		public EpochRecord[] GetLastEpochs(int maxCount) {
			lock (_locker) {
				var res = new List<EpochRecord>();
				var node = _epochs.Last;
				while (node != null && res.Count < maxCount) {
					res.Add(node.Value);
					node = node.Previous;
				}

				return res.ToArray();
			}
		}

		public EpochRecord GetEpochAfter(int epochNumber, bool throwIfNotFound) {
			if (epochNumber >= LastEpochNumber) {
				if (!throwIfNotFound)
					return null;
				throw new ArgumentOutOfRangeException(
					nameof(epochNumber),
					$"EpochNumber no epochs exist after Last Epoch {epochNumber}.");
			}

			EpochRecord epoch;
			lock (_locker) {
				var epochNode = _epochs.Last;
				while (epochNode != null && epochNode.Value.EpochNumber != epochNumber) {
					epochNode = epochNode.Previous;
				}

				epoch = epochNode?.Next?.Value;
			}

			if (epoch != null) {
				return epoch; //got it
			}

			var firstEpoch = _firstCachedEpoch?.Value;
			if (firstEpoch != null && firstEpoch.PrevEpochPosition != -1) {
				var reader = _readers.Get();
				try {
					epoch = firstEpoch;
					do {
						var result = reader.TryReadAt(epoch.PrevEpochPosition);
						if (!result.Success)
							throw new Exception(
								$"Could not find Epoch record at LogPosition {epoch.PrevEpochPosition}.");
						if (result.LogRecord.RecordType != LogRecordType.System)
							throw new Exception($"LogRecord is not SystemLogRecord: {result.LogRecord}.");

						var sysRec = (ISystemLogRecord)result.LogRecord;
						if (sysRec.SystemRecordType != SystemRecordType.Epoch)
							throw new Exception($"SystemLogRecord is not of Epoch sub-type: {result.LogRecord}.");

						var nextEpoch = sysRec.GetEpochRecord();
						if (nextEpoch.EpochNumber == epochNumber) {
							return epoch; //got it
						}

						epoch = nextEpoch;
					} while (epoch.PrevEpochPosition != -1 && epoch.EpochNumber > epochNumber);

				} finally {
					_readers.Return(reader);
				}
			}
			
			if (epoch == null && throwIfNotFound) {
				throw new Exception($"Concurrency failure, epoch #{epochNumber} should not be null.");
			}

			return epoch;
		}

		public bool IsCorrectEpochAt(long epochPosition, int epochNumber, Guid epochId) {
			Ensure.Nonnegative(epochPosition, "logPosition");
			Ensure.Nonnegative(epochNumber, "epochNumber");
			Ensure.NotEmptyGuid(epochId, "epochId");


			if (epochNumber > LastEpochNumber)
				return false;

			EpochRecord epoch;
			lock (_locker) {
				epoch = _epochs.FirstOrDefault(e => e.EpochNumber == epochNumber);
				if (epoch != null) {
					return epoch.EpochId == epochId && epoch.EpochPosition == epochPosition;
				}
			}

			// epochNumber < _minCachedEpochNumber
			var reader = _readers.Get();
			try {
				var res = reader.TryReadAt(epochPosition);
				if (!res.Success || res.LogRecord.RecordType != LogRecordType.System)
					return false;
				var sysRec = (ISystemLogRecord)res.LogRecord;
				if (sysRec.SystemRecordType != SystemRecordType.Epoch)
					return false;

				epoch = sysRec.GetEpochRecord();
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
			if (epochNumber < 0) {
				throw new ArgumentException($"Cannot write an Epoch with a negative Epoch Number {epochNumber}.",
					nameof(epochNumber));
			}

			if (epochNumber <= LastEpochNumber) {
				throw new ArgumentException(
					$"Cannot add Epoch {epochNumber}, new Epoch numbers must be greater than the Last Epoch  {LastEpochNumber}.",
					nameof(epochNumber));
			}

			var epoch = WriteEpochRecordWithRetry(epochNumber, Guid.NewGuid(),
				_lastCachedEpoch?.Value.EpochPosition ?? -1, _instanceId);
			AddEpochToCache(epoch);
		}

		private EpochRecord WriteEpochRecordWithRetry(int epochNumber, Guid epochId, long lastEpochPosition,
			Guid instanceId) {
			long pos = _writer.Checkpoint.ReadNonFlushed();
			var epoch = new EpochRecord(pos, epochNumber, epochId, lastEpochPosition, DateTime.UtcNow, instanceId);
			var rec = _recordFactory.CreateEpoch(epoch);

			Log.Debug(
							"=== Writing E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}). L={leaderId:B}.",
							epochNumber, epoch.EpochPosition, epochId, lastEpochPosition, epoch.LeaderInstanceId);
			if (!_writer.Write(rec, out pos)) {
				epoch = new EpochRecord(pos, epochNumber, epochId, lastEpochPosition, DateTime.UtcNow, instanceId);
				rec = _recordFactory.CreateEpoch(epoch);

				if (!_writer.Write(rec, out pos))
					throw new Exception($"Second write try failed at {epoch.EpochPosition}.");
			}
			_writer.Flush();
			_bus.Publish(new SystemMessage.EpochWritten(epoch));
			return epoch;
		}

		public void CacheEpoch(EpochRecord epoch) {
			var added = AddEpochToCache(epoch);
			// Check each epoch as it is added to the cache for the first time from the chaser.
			// n.b.: added will be false for idempotent CacheRequests
			// If this check fails, then there is something very wrong with epochs, data corruption is possible.
			if (added && !IsCorrectEpochAt(epoch.EpochPosition, epoch.EpochNumber, epoch.EpochId)) {
				throw new Exception(
					$"Not found epoch at {epoch.EpochPosition} with epoch number: {epoch.EpochNumber} and epoch ID: {epoch.EpochId}. " +
					"SetLastEpoch FAILED! Data corruption risk!");
			}
		}
		/// <summary>
		/// Idempotently adds epochs to the cache
		/// </summary>
		/// <param name="epoch">the epoch to add</param>
		/// <returns>if the submitted epoch was added to the cache, false if already present</returns>
		public bool AddEpochToCache(EpochRecord epoch) {
			Ensure.NotNull(epoch, "epoch");

			lock (_locker) {

				// if it's already cached, just return false to indicate idempotent add 
				if (_epochs.Contains(ep => ep.EpochNumber == epoch.EpochNumber)) { return false; }

				//new last epoch written or received, this is the normal case
				//if the list is empty Last will be null
				if (_epochs.Last == null || _epochs.Last.Value.EpochNumber < epoch.EpochNumber) {
					_epochs.AddLast(epoch);
					_lastCachedEpoch = _epochs.Last;
					// in some race conditions we might have a gap in the epoch list
					//read the epochs from the TFLog to fill in the gaps
					if (epoch.EpochPosition > 0 &&
						epoch.PrevEpochPosition >= 0 && 						
						epoch.PrevEpochPosition > (_epochs.Last?.Previous?.Value?.EpochPosition ?? -1)) {
						var reader = _readers.Get();
						var previous = _epochs.Last;
						var count = 1; //include last
						try {
							do {
								epoch = ReadEpochAt(reader, epoch.PrevEpochPosition);
								previous = _epochs.AddBefore(previous, epoch);
								count++;
							} while (
								epoch.EpochPosition > 0 &&
								epoch.PrevEpochPosition >= 0 &&
								count <= _cacheSize &&
								epoch.PrevEpochPosition > (previous?.Previous?.Value?.EpochPosition ?? -1));
						} finally {
							_readers.Return(reader);
						}
					}
					while (_epochs.Count > _cacheSize) { _epochs.RemoveFirst(); }
					_firstCachedEpoch = _epochs.First;
					// Now update epoch checkpoint, so on restart we don't scan sequentially TF.
					_checkpoint.Write(_epochs.Last.Value.EpochPosition);
					_checkpoint.Flush();
					Log.Debug(
						"=== Cached new Last Epoch E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}) L={leaderId:B}.",
						epoch.EpochNumber, epoch.EpochPosition, epoch.EpochId, epoch.PrevEpochPosition, epoch.LeaderInstanceId);
					return true;
				}				
				if (epoch.EpochNumber < _epochs.First.Value.EpochNumber) {					
					return false;
				}				
				//this should never happen
				Log.Error("=== Unable to cache Epoch E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}) L={leaderId:B}.",
					epoch.EpochNumber, epoch.EpochPosition, epoch.EpochId, epoch.PrevEpochPosition, epoch.LeaderInstanceId);
				foreach (var epochRecord in _epochs) {
					Log.Error(
						"====== Epoch E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}) L={leaderId:B}.",
						epochRecord.EpochNumber, epochRecord.EpochPosition, epochRecord.EpochId, epochRecord.PrevEpochPosition, epochRecord.LeaderInstanceId);
				}

				Log.Error(
					"====== Last Epoch E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}) L={leaderId:B}.",
					_lastCachedEpoch.Value.EpochNumber, _lastCachedEpoch.Value.EpochPosition, _lastCachedEpoch.Value.EpochId, _lastCachedEpoch.Value.PrevEpochPosition, _lastCachedEpoch.Value.LeaderInstanceId);
				Log.Error(
					"====== First Epoch E{epochNumber}@{epochPosition}:{epochId:B} (previous epoch at {lastEpochPosition}) L={leaderId:B}.",
					_firstCachedEpoch.Value.EpochNumber, _firstCachedEpoch.Value.EpochPosition, _firstCachedEpoch.Value.EpochId, _firstCachedEpoch.Value.PrevEpochPosition, _firstCachedEpoch.Value.LeaderInstanceId);

				throw new Exception($"This should never happen: Unable to find correct position to cache Epoch E{epoch.EpochNumber}@{epoch.EpochPosition}:{epoch.EpochId:B} (previous epoch at {epoch.PrevEpochPosition}) L={epoch.LeaderInstanceId:B}");
			}
		}


	}
}
