using System;
using System.Collections.Concurrent;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.EpochManager
{
    public interface IEpochManager
    {
        int EpochCount { get; }

        void Init();
        EpochRecord GetLastEpoch();
        EpochRecord GetEpoch(int epochNumber);

        void SetLastEpoch(int epochNumber, Guid epochId);
    }

    public class EpochManager: IEpochManager
    {
        public const int CachedEpochCount = 100;

        public int EpochCount { get { return _epochCount; } }

        private readonly ConcurrentDictionary<int, EpochRecord> _epochs = new ConcurrentDictionary<int, EpochRecord>();
        private readonly ICheckpoint _checkpoint;
        private readonly ITransactionFileReader _reader;
        private readonly ITransactionFileSequentialReader _seqReader;
        private readonly ITransactionFileWriter _writer;

        private volatile int _epochCount;
        private volatile int _minCachedEpochNumber = int.MaxValue;

        public EpochManager(ICheckpoint checkpoint, 
                            ITransactionFileReader reader, 
                            ITransactionFileSequentialReader seqReader, 
                            ITransactionFileWriter writer)
        {
            Ensure.NotNull(checkpoint, "checkpoint");
            Ensure.NotNull(reader, "reader");
            Ensure.NotNull(seqReader, "seqReader");
            Ensure.NotNull(writer, "chunkWriter");

            _checkpoint = checkpoint;
            _reader = reader;
            _seqReader = seqReader;
            _writer = writer;
        }

        public void Init()
        {
            long epochPos = _checkpoint.Read();
            if (epochPos < 0) // we probably have lost/uninitialized epoch checkpoint
            {
                _seqReader.Reposition(_writer.Checkpoint.Read());

                SeqReadResult result;
                while ((result = _seqReader.TryReadPrev()).Success)
                {
                    if (result.LogRecord.RecordType != LogRecordType.System || ((SystemLogRecord) result.LogRecord).SystemRecordType != SystemRecordType.Epoch)
                        continue;
                    epochPos = result.RecordPrePosition;
                    break;
                }
            }

            int cnt = 0;
            while (epochPos >= 0 && cnt < CachedEpochCount)
            {
                var result = _reader.TryReadAt(epochPos);
                if (!result.Success)
                    throw new Exception(string.Format("Couldn't find Epoch record at LogPosition {0}.", epochPos));
                if (result.LogRecord.RecordType != LogRecordType.System 
                    || ((SystemLogRecord) result.LogRecord).SystemRecordType != SystemRecordType.Epoch)
                    throw new Exception(string.Format("Epoch LogRecord is of unexpected type: {0}.", result.LogRecord));

                var epoch = ((SystemLogRecord)result.LogRecord).GetEpochRecord();
                _epochs.AddOrUpdate(epoch.EpochNumber, epoch, (i, record) => epoch);
                _epochCount = Math.Max(_epochCount, epoch.EpochNumber + 1);
                _minCachedEpochNumber = Math.Min(_minCachedEpochNumber, epoch.EpochNumber);
                cnt += 1;

                epochPos = epoch.PrevEpochPosition;
            }
        }

        public EpochRecord GetLastEpoch()
        {
            return GetEpoch(_epochCount - 1);
        }

        public EpochRecord GetEpoch(int epochNumber)
        {
            if (epochNumber < _minCachedEpochNumber)
            {
                throw new ArgumentOutOfRangeException(
                        "epochNumber",
                        string.Format("EpochNumber requested shouldn't be cached. Requested: {0}, min cached: {1}.",
                                      epochNumber,
                                      _minCachedEpochNumber));
            }
            var epoch = _epochs[epochNumber];
            if (epoch == null)
                throw new Exception(string.Format("Concurrency failure, epoch #{0} shouldn't be null.", epochNumber));
            return epoch;
        }

        public void SetLastEpoch(int epochNumber, Guid epochId)
        {
            // This method should be called from single thread.
            Ensure.NotEmptyGuid(epochId, "epochId");

            // if we are writing the very first epoch, last position will be -1.
            long lastEpochPosition = epochNumber == 0 ? -1 : GetEpoch(epochNumber - 1).LogPosition; 

            // Set epoch checkpoint to -1, so if we crash after new epoch record was written, 
            // but epoch checkpoint wasn't updated, on restart we don't miss the latest epoch.
            // So on node start, if there is no epoch checkpoint or it contains negative position, 
            // we do sequential scan from the end of TF to find the latest epoch record.
            _checkpoint.Write(-1);
            _checkpoint.Flush();

            // Now we write epoch record (with possible retry, if we are at the end of chunk) 
            // and update EpochManager's state, by adjusting cache of records, epoch count and un-caching 
            // excessive record, if present
            var epoch = WriteEpochRecordWithRetry(epochNumber, epochId, lastEpochPosition);
            _epochs.AddOrUpdate(epochNumber, epoch, (_, __) => epoch);
            _epochCount = epochNumber + 1;
            _minCachedEpochNumber = Math.Max(_minCachedEpochNumber, epochNumber - CachedEpochCount + 1);
            EpochRecord tmp;
            _epochs.TryRemove(_minCachedEpochNumber - 1, out tmp);

            // Now update epoch checkpoint, so on restart we don't scan sequentially TF.
            _checkpoint.Write(epoch.LogPosition);
            _checkpoint.Flush();
        }

        private EpochRecord WriteEpochRecordWithRetry(int epochNumber, Guid epochId, long lastEpochPosition)
        {
            long pos = _writer.Checkpoint.ReadNonFlushed();
            var epoch = new EpochRecord(pos, DateTime.UtcNow, epochNumber, epochId, lastEpochPosition);
            var rec = new SystemLogRecord(epoch.LogPosition, epoch.TimeStamp, SystemRecordType.Epoch, SystemRecordSerialization.Json, epoch.AsSerialized());

            if (!_writer.Write(rec, out pos))
            {
                epoch = new EpochRecord(pos, DateTime.UtcNow, epochNumber, epochId, lastEpochPosition);
                rec = new SystemLogRecord(epoch.LogPosition, epoch.TimeStamp, SystemRecordType.Epoch, SystemRecordSerialization.Json, epoch.AsSerialized());
                if (!_writer.Write(rec, out pos))
                    throw new Exception(string.Format("Second write try failed at {0}.", epoch.LogPosition));
            }
            _writer.Flush();

            return epoch;
        }
    }
}
