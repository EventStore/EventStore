// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Collections.Generic;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using System.Linq;

namespace EventStore.Core.Services.Storage.EpochManager
{
    public class EpochManager: IEpochManager
    {
        public readonly int CachedEpochCount;
        public int LastEpochNumber { get { return _lastEpochNumber; } }

        private readonly ICheckpoint _checkpoint;
        private readonly ObjectPool<ITransactionFileReader> _readers;
        private readonly ITransactionFileWriter _writer;

        private readonly object _locker = new object();
        private readonly Dictionary<int, EpochRecord> _epochs = new Dictionary<int, EpochRecord>();
        private volatile int _lastEpochNumber = -1;
        private long _lastEpochPosition = -1;
        private int _minCachedEpochNumber = -1;

        public EpochManager(int cachedEpochCount, 
                            ICheckpoint checkpoint, 
                            ITransactionFileWriter writer,
                            int initialReaderCount,
                            int maxReaderCount, 
                            Func<ITransactionFileReader> readerFactory)
        {
            Ensure.Nonnegative(cachedEpochCount, "cachedEpochCount");
            Ensure.NotNull(checkpoint, "checkpoint");
            Ensure.NotNull(writer, "chunkWriter");
            Ensure.Nonnegative(initialReaderCount, "initialReaderCount");
            Ensure.Positive(maxReaderCount, "maxReaderCount");
            if (initialReaderCount > maxReaderCount)
                throw new ArgumentOutOfRangeException("initialReaderCount", "initialReaderCount is greater than maxReaderCount.");
            Ensure.NotNull(readerFactory, "readerFactory");

            CachedEpochCount = cachedEpochCount;
            _checkpoint = checkpoint;
            _readers = new ObjectPool<ITransactionFileReader>("EpochManager readers pool", initialReaderCount, maxReaderCount, readerFactory);
            _writer = writer;
        }

        public void Init()
        {
            lock (_locker)
            {
                var reader = _readers.Get();
                try
                {
                    long epochPos = _checkpoint.Read();
                    if (epochPos < 0) // we probably have lost/uninitialized epoch checkpoint
                    {
                        reader.Reposition(_writer.Checkpoint.Read());

                        SeqReadResult result;
                        while ((result = reader.TryReadPrev()).Success)
                        {
                            var rec = result.LogRecord;
                            if (rec.RecordType != LogRecordType.System || ((SystemLogRecord)rec).SystemRecordType != SystemRecordType.Epoch)
                                continue;
                            epochPos = rec.Position;
                            break;
                        }
                    }

                    int cnt = 0;
                    while (epochPos >= 0 && cnt < CachedEpochCount)
                    {
                        var result = reader.TryReadAt(epochPos);
                        if (!result.Success)
                            throw new Exception(string.Format("Couldn't find Epoch record at LogPosition {0}.", epochPos));
                        if (result.LogRecord.RecordType != LogRecordType.System)
                            throw new Exception(string.Format("LogRecord is not SystemLogRecord: {0}.", result.LogRecord));
                        
                        var sysRec = (SystemLogRecord) result.LogRecord;
                        if (sysRec.SystemRecordType != SystemRecordType.Epoch)
                            throw new Exception(string.Format("SystemLogRecord is not of Epoch sub-type: {0}.", result.LogRecord));

                        var epoch = sysRec.GetEpochRecord();
                        _epochs[epoch.EpochNumber] = epoch;
                        _lastEpochNumber = Math.Max(_lastEpochNumber, epoch.EpochNumber);
                        _lastEpochPosition = Math.Max(_lastEpochPosition, epoch.EpochPosition);
                        _minCachedEpochNumber = epoch.EpochNumber;

                        epochPos = epoch.PrevEpochPosition;
                        cnt += 1;
                    }
                }
                finally
                {
                    _readers.Return(reader);
                }
            }
        }

        public EpochRecord GetLastEpoch()
        {
            lock (_locker)
            {
                return _lastEpochNumber < 0 ? null : GetEpoch(_lastEpochNumber, throwIfNotFound: true);
            }
        }

        public EpochRecord GetEpoch(int epochNumber, bool throwIfNotFound)
        {
            lock (_locker)
            {
                if (epochNumber < _minCachedEpochNumber)
                {
                    if (!throwIfNotFound)
                        return null;
                    throw new ArgumentOutOfRangeException(
                            "epochNumber",
                            string.Format("EpochNumber requested shouldn't be cached. Requested: {0}, min cached: {1}.",
                                            epochNumber,
                                            _minCachedEpochNumber));
                }
                EpochRecord epoch;
                if (!_epochs.TryGetValue(epochNumber, out epoch) && throwIfNotFound)
                    throw new Exception(string.Format("Concurrency failure, epoch #{0} shouldn't be null.", epochNumber));
                return epoch;
            }
        }

        public bool IsCorrectEpochAt(long logPosition, int epochNumber, Guid epochId)
        {
            Ensure.Nonnegative(logPosition, "logPosition");
            Ensure.Nonnegative(epochNumber, "epochNumber");
            Ensure.NotEmptyGuid(epochId, "epochId");

            lock (_locker)
            {
                if (epochNumber > _lastEpochNumber)
                    return false;
                if (epochNumber >= _minCachedEpochNumber)
                {
                    var epoch = _epochs[epochNumber];
                    return epoch.EpochId == epochId && epoch.EpochPosition == logPosition;
                }
            }
            
            // epochNumber < _minCachedEpochNumber
            var reader = _readers.Get();
            try
            {
                var res = reader.TryReadAt(logPosition);
                if (!res.Success || res.LogRecord.RecordType != LogRecordType.System)
                    return false;
                var sysRec = (SystemLogRecord) res.LogRecord;
                if (sysRec.SystemRecordType != SystemRecordType.Epoch)
                    return false;

                var epoch = sysRec.GetEpochRecord();
                return epoch.EpochNumber == epochNumber && epoch.EpochId == epochId;
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        // This method should be called from single thread.
        public void WriteNewEpoch()
        {
            // Set epoch checkpoint to -1, so if we crash after new epoch record was written, 
            // but epoch checkpoint wasn't updated, on restart we don't miss the latest epoch.
            // So on node start, if there is no epoch checkpoint or it contains negative position, 
            // we do sequential scan from the end of TF to find the latest epoch record.
            //NOTE AN: It seems we don't need to pessimistically set epoch checkpoint to -1, because
            //NOTE AN: if crash occurs in the middle of writing epoch or updating epoch checkpoint,
            //NOTE AN: then on restart we'll start from chaser checkpoint (which is not updated yet)
            //NOTE AN: and process all records till the writer checkpoint, so all epochs will be processed 
            //NOTE AN: and epoch checkpoint will ultimately contain correct last epoch position. This process
            //NOTE AN: is similar to index rebuild process.
            //_checkpoint.Write(-1);
            //_checkpoint.Flush();

            // Now we write epoch record (with possible retry, if we are at the end of chunk) 
            // and update EpochManager's state, by adjusting cache of records, epoch count and un-caching 
            // excessive record, if present.
            // If we are writing the very first epoch, last position will be -1.
            var epoch = WriteEpochRecordWithRetry(_lastEpochNumber + 1, Guid.NewGuid(), _lastEpochPosition);
            UpdateLastEpoch(epoch);
        }

        private EpochRecord WriteEpochRecordWithRetry(int epochNumber, Guid epochId, long lastEpochPosition)
        {
            long pos = _writer.Checkpoint.ReadNonFlushed();
            var epoch = new EpochRecord(pos, epochNumber, epochId, lastEpochPosition, DateTime.UtcNow);
            var rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch, SystemRecordSerialization.Json, epoch.AsSerialized());

            if (!_writer.Write(rec, out pos))
            {
                epoch = new EpochRecord(pos, epochNumber, epochId, lastEpochPosition, DateTime.UtcNow);
                rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch, SystemRecordSerialization.Json, epoch.AsSerialized());
                if (!_writer.Write(rec, out pos))
                    throw new Exception(string.Format("Second write try failed at {0}.", epoch.EpochPosition));
            }
            _writer.Flush();

            return epoch;
        }

        public void SetLastEpoch(EpochRecord epoch)
        {
            Ensure.NotNull(epoch, "epoch");

            lock (_locker)
            {
                if (epoch.EpochPosition > _lastEpochPosition)
                {
                    UpdateLastEpoch(epoch);
                    return;
                }
            }

            // Epoch record must have been already written, so we need to make sure it is where we expect it to be.
            // If this check fails, then there is something very wrong with epochs, data corruption is possible.
            if (!IsCorrectEpochAt(epoch.EpochPosition, epoch.EpochNumber, epoch.EpochId))
            {
                throw new Exception(string.Format("Not found epoch at {0} with epoch number: {1} and epoch ID: {2}. "
                                                  + "SetLastEpoch FAILED! Data corruption risk!",
                                                  epoch.EpochPosition,
                                                  epoch.EpochNumber,
                                                  epoch.EpochId));
            }
        }

        public IEnumerable<EpochRecord> GetCachedEpochs()
        {
            lock (_locker)
            {
                return _epochs.Values.ToArray();
            }
        }

        private void UpdateLastEpoch(EpochRecord epoch)
        {
            lock (_locker)
            {
                _epochs[epoch.EpochNumber] = epoch;
                _lastEpochNumber = epoch.EpochNumber;
                _lastEpochPosition = epoch.EpochPosition;
                _minCachedEpochNumber = Math.Max(_minCachedEpochNumber, epoch.EpochNumber - CachedEpochCount + 1);
                _epochs.Remove(_minCachedEpochNumber - 1);

                // Now update epoch checkpoint, so on restart we don't scan sequentially TF.
                _checkpoint.Write(epoch.EpochPosition);
                _checkpoint.Flush();
            }
        }
    }
}