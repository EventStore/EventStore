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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using Newtonsoft.Json.Linq;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public class ReadIndex : IDisposable, IReadIndex
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ReadIndex>();
        private static readonly EventRecord[] EmptyRecords = new EventRecord[0];

        public long LastCommitPosition { get { return Interlocked.Read(ref _lastCommitPosition); } }

        private long _succReadCount;
        private long _failedReadCount;

#if __MonoCS__
        private readonly Common.ConcurrentCollections.ConcurrentStack<ITransactionFileReader> _readers = new Common.ConcurrentCollections.ConcurrentStack<ITransactionFileReader>();
#else
        private readonly System.Collections.Concurrent.ConcurrentStack<ITransactionFileReader> _readers = new System.Collections.Concurrent.ConcurrentStack<ITransactionFileReader>();
#endif
#if __MonoCS__
        private readonly Common.ConcurrentCollections.ConcurrentStack<ITransactionFileSequentialReader> _seqReaders = new Common.ConcurrentCollections.ConcurrentStack<ITransactionFileSequentialReader>();
#else
        private readonly System.Collections.Concurrent.ConcurrentStack<ITransactionFileSequentialReader> _seqReaders = new System.Collections.Concurrent.ConcurrentStack<ITransactionFileSequentialReader>();
#endif
        private readonly ITableIndex _tableIndex;
        private readonly IHasher _hasher;
        private readonly IPublisher _bus;
        private readonly ILRUCache<string, StreamMetadata> _metadataCache;

        private long _persistedPrepareCheckpoint = -1;
        private long _persistedCommitCheckpoint = -1;
        private long _lastCommitPosition = -1;
        private bool _indexRebuild = true;

        private readonly BoundedCache<Guid, Tuple<string, int>> _committedEvents = 
            new BoundedCache<Guid, Tuple<string, int>>(int.MaxValue, 10*1024*1024, x => 16 + 4 + 2*x.Item1.Length);

        public ReadIndex(IPublisher bus,
                         int readerCount,
                         Func<ITransactionFileSequentialReader> seqReaderFactory,
                         Func<ITransactionFileReader> readerFactory,
                         ITableIndex tableIndex,
                         IHasher hasher,
                         ILRUCache<string, StreamMetadata> metadataCache)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.Positive(readerCount, "readerCount");
            Ensure.NotNull(seqReaderFactory, "seqReaderFactory");
            Ensure.NotNull(readerFactory, "readerFactory");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(hasher, "hasher");
            Ensure.NotNull(metadataCache, "metadataCache");

            _bus = bus;
            _tableIndex = tableIndex;
            _hasher = hasher;
            _metadataCache = metadataCache;

            for (int i = 0; i < readerCount; ++i)
            {
                _seqReaders.Push(seqReaderFactory());
                _readers.Push(readerFactory());
            }
        }

        private ITransactionFileReader GetReader()
        {
            ITransactionFileReader reader;
            if (!_readers.TryPop(out reader))
                throw new InvalidOperationException("Unable to acquire reader in ReadIndex.");
            return reader;
        }

        private ITransactionFileSequentialReader GetSeqReader()
        {
            ITransactionFileSequentialReader seqReader;
            if (!_seqReaders.TryPop(out seqReader))
                throw new InvalidOperationException("Unable to acquire sequential reader in ReadIndex.");
            return seqReader;
        }

        private void ReturnReader(ITransactionFileReader reader)
        {
            _readers.Push(reader);
        }

        private void ReturnSeqReader(ITransactionFileSequentialReader seqReader)
        {
            _seqReaders.Push(seqReader);
        }

        public void Build()
        {
            _tableIndex.Initialize();
            _persistedPrepareCheckpoint = _tableIndex.PrepareCheckpoint;
            _persistedCommitCheckpoint = _tableIndex.CommitCheckpoint;
            _lastCommitPosition = _tableIndex.CommitCheckpoint;

            foreach (var rdr in _readers)
            {
                rdr.Open();
            }
            foreach (var seqRdr in _seqReaders)
            {
                seqRdr.Open();
            }

            var seqReader = GetSeqReader();
            try
            {
                seqReader.Reposition(Math.Max(0, _persistedCommitCheckpoint));

                long processed = 0;
                SeqReadResult result;
                while ((result = seqReader.TryReadNext()).Success)
                {
                    if (result.LogRecord.RecordType == LogRecordType.Commit)
                        Commit((CommitLogRecord)result.LogRecord);

                    processed += 1;
                    if (processed % 100000 == 0)
                        Log.Debug("ReadIndex Rebuilding: processed {0} records.", processed);
                }
            }
            finally
            {
                ReturnSeqReader(seqReader);
            }

            _indexRebuild = false;
        }

        public void Commit(CommitLogRecord commit)
        {
            if (commit.LogPosition < _lastCommitPosition || (commit.LogPosition == _lastCommitPosition && !_indexRebuild))
                return;  // already committed

            bool first = true;
            int eventNumber = -1;
            uint streamHash = 0;
            string eventStreamId = null;

            foreach (var prepare in GetTransactionPrepares(commit.TransactionPosition))
            {
                if (first)
                {
                    streamHash = _hasher.Hash(prepare.EventStreamId);
                    eventStreamId = prepare.EventStreamId;
                    first = false;
                }
                else
                    Debug.Assert(prepare.EventStreamId == eventStreamId);

                bool addToIndex = false;
                if ((prepare.Flags & PrepareFlags.StreamDelete) != 0)
                {
                    eventNumber = EventNumber.DeletedStream;
                    _committedEvents.PutRecord(prepare.EventId, Tuple.Create(eventStreamId, eventNumber), throwOnDuplicate: false);
                    addToIndex = commit.LogPosition > _persistedCommitCheckpoint
                                 || commit.LogPosition == _persistedCommitCheckpoint && prepare.LogPosition > _persistedPrepareCheckpoint;
                }
                else if ((prepare.Flags & PrepareFlags.Data) != 0)
                {
                    eventNumber = commit.EventNumber + prepare.TransactionOffset;
                    _committedEvents.PutRecord(prepare.EventId, Tuple.Create(eventStreamId, eventNumber), throwOnDuplicate: false);
                    addToIndex = commit.LogPosition > _persistedCommitCheckpoint
                                 || commit.LogPosition == _persistedCommitCheckpoint && prepare.LogPosition > _persistedPrepareCheckpoint;
                }

                // could be just empty prepares for TransactionBegin and TransactionEnd, for instance
                // or records which are rebuilt but are already in PTables
                if (addToIndex)
                {
#if DEBUG
                    long pos;
                    if (_tableIndex.TryGetOneValue(streamHash, eventNumber, out pos))
                    {
                        EventRecord rec;
                        if (((IReadIndex)this).ReadEvent(eventStreamId, eventNumber, out rec) == SingleReadResult.Success)
                        {
                            Debugger.Break();
                            throw new Exception(
                                string.Format(
                                    "Trying to add duplicate event #{0} for stream {1}(hash {2})\nCommit: {3}\nPrepare: {4}.",
                                    eventNumber,
                                    eventStreamId,
                                    streamHash,
                                    commit,
                                    prepare));
                        }
                    }
#endif
                    _tableIndex.Add(commit.LogPosition, streamHash, eventNumber, prepare.LogPosition);
                    _bus.Publish(new StorageMessage.EventCommited(commit.LogPosition, eventNumber, prepare));
                }
                _lastCommitPosition = Math.Max(_lastCommitPosition, commit.LogPosition);
            }
        }

        private IEnumerable<PrepareLogRecord> GetTransactionPrepares(long transactionPos)
        {
            bool first = true;
            var seqReader = GetSeqReader();
            try
            {
                seqReader.Reposition(transactionPos);

                while (true)
                {
                    var result = seqReader.TryReadNext();
                    if (!result.Success)
                        throw new Exception("Couldn't read record which is supposed to be in file.");

                    if (first && result.LogRecord.RecordType != LogRecordType.Prepare)
                        throw new Exception(string.Format("The first transaction record is not prepare: {0}. ", result.LogRecord.RecordType));
                    first = false;

                    var prepare = result.LogRecord as PrepareLogRecord;
                    if (prepare != null && prepare.TransactionPosition == transactionPos)
                    {
                        yield return prepare;
                        if ((prepare.Flags & PrepareFlags.TransactionEnd) != 0)
                            yield break;
                    }
                }
            }
            finally
            {
                ReturnSeqReader(seqReader);
            }
        }

        SingleReadResult IReadIndex.ReadEvent(string streamId, int eventNumber, out EventRecord record)
        {
            var reader = GetReader();
            try
            {
                return ReadEventInternal(reader, streamId, eventNumber, out record);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private SingleReadResult ReadEventInternal(ITransactionFileReader reader, string streamId, int version, out EventRecord record)
        {
            Ensure.NotNull(streamId, "streamId");
            Ensure.Nonnegative(version, "eventNumber");

            record = null;
            if (IsStreamDeletedInternal(reader, streamId))
                return SingleReadResult.StreamDeleted;

            StreamMetadata metadata;
            bool streamExists;
            bool useMetadata = GetStreamMetadataInternal(reader, streamId, out streamExists, out metadata);
            if (!streamExists)
                return SingleReadResult.NoStream;

            if (useMetadata && metadata.MaxCount.HasValue)
            {
                var lastStreamEventNumber = GetLastStreamEventNumberInternal(reader, streamId);
                var minEventNumber = lastStreamEventNumber - metadata.MaxCount.Value + 1;

                if (version < minEventNumber || version > lastStreamEventNumber)
                    return SingleReadResult.NotFound;
            }

            EventRecord rec;
            var success = GetStreamRecord(reader, streamId, version, out rec);
            if (success)
            {
                if (useMetadata && metadata.MaxAge.HasValue && rec.TimeStamp < DateTime.UtcNow - metadata.MaxAge.Value)
                    return SingleReadResult.NotFound;
                record = rec;
                return SingleReadResult.Success;
            }

            return SingleReadResult.NotFound;
        }

        RangeReadResult IReadIndex.ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount, out EventRecord[] records)
        {
            Ensure.NotNull(streamId, "streamId");
            Ensure.Nonnegative(fromEventNumber, "fromEventNumber");
            Ensure.Positive(maxCount, "maxCount");

            records = EmptyRecords;
            var streamHash = _hasher.Hash(streamId);
            var reader = GetReader();
            try
            {
                if (IsStreamDeletedInternal(reader, streamId))
                    return RangeReadResult.StreamDeleted;

                StreamMetadata metadata;
                bool streamExists;
                bool useMetadata = GetStreamMetadataInternal(reader, streamId, out streamExists, out metadata);
                if (!streamExists)
                    return RangeReadResult.NoStream;

                int startEventNumber = fromEventNumber;
                int endEventNumber = (int) Math.Min(int.MaxValue, (long) fromEventNumber + maxCount - 1);

                if (useMetadata && metadata.MaxCount.HasValue)
                {
                    var lastStreamEventNumber = GetLastStreamEventNumberInternal(reader, streamId);
                    var minEventNumber = lastStreamEventNumber - metadata.MaxCount.Value + 1;

                    if (minEventNumber > endEventNumber)
                        return RangeReadResult.Success;
                    startEventNumber = Math.Max(startEventNumber, minEventNumber);
                }

                var recordsQuery = _tableIndex.GetRange(streamHash, startEventNumber, endEventNumber)
                                              .Select(x => GetEventRecord(reader, x))
                                              .Where(x => x.EventStreamId == streamId);

                if (useMetadata && metadata.MaxAge.HasValue)
                {
                    var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
                    recordsQuery = recordsQuery.Where(x => x.TimeStamp >= ageThreshold);
                }

                records = recordsQuery.Reverse().ToArray();
                return RangeReadResult.Success;
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        RangeReadResult IReadIndex.ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount, out EventRecord[] records)
        {
            Ensure.NotNull(streamId, "streamId");
            Ensure.Positive(maxCount, "maxCount");

            records = EmptyRecords;
            var streamHash = _hasher.Hash(streamId);

            var reader = GetReader();
            try
            {
                if (IsStreamDeletedInternal(reader, streamId))
                    return RangeReadResult.StreamDeleted;

                int lastStreamEventNumber = int.MinValue;
                int endEventNumber = fromEventNumber;
                if (endEventNumber < 0)
                {
                    lastStreamEventNumber = GetLastStreamEventNumberInternal(reader, streamId);
                    endEventNumber = lastStreamEventNumber;
                    if (lastStreamEventNumber == -1) // optimization to reduce index lookups
                        return RangeReadResult.NoStream;
                }

                var startEventNumber = Math.Max(0, endEventNumber - maxCount + 1);
                StreamMetadata metadata;
                bool streamExists;
                bool useMetadata = GetStreamMetadataInternal(reader, streamId, out streamExists, out metadata);
                if (!streamExists)
                    return RangeReadResult.NoStream;

                if (useMetadata && metadata.MaxCount.HasValue)
                {
                    if (lastStreamEventNumber == int.MinValue) // not loaded yet
                        lastStreamEventNumber = GetLastStreamEventNumberInternal(reader, streamId);
                    var minEventNumber = lastStreamEventNumber - metadata.MaxCount.Value + 1;

                    if (minEventNumber > endEventNumber)
                        return RangeReadResult.Success;
                    startEventNumber = Math.Max(startEventNumber, minEventNumber);
                }

                var recordsQuery = _tableIndex.GetRange(streamHash, startEventNumber, endEventNumber)
                                              .Select(x => GetEventRecord(reader, x))
                                              .Where(x => x.EventStreamId == streamId);

                if (useMetadata && metadata.MaxAge.HasValue)
                {
                    var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
                    recordsQuery = recordsQuery.Where(x => x.TimeStamp >= ageThreshold);
                }

                records = recordsQuery.ToArray();
                return RangeReadResult.Success;
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private bool GetStreamRecord(ITransactionFileReader reader, string streamId, int version, out EventRecord record)
        {
            // we assume that you already did check for stream deletion
            Ensure.NotNull(streamId, "streamId");
            Ensure.Nonnegative(version, "eventNumber");

            var streamHash = _hasher.Hash(streamId);

            long position;
            if (_tableIndex.TryGetOneValue(streamHash, version, out position))
            {
                EventRecord rec = GetEventRecord(reader, new IndexEntry(streamHash, version, position));
                if (rec.EventStreamId == streamId)
                {
                    _succReadCount += 1;
                    record = rec;
                    return true;
                }
                _failedReadCount += 1;

                foreach (var indexEntry in _tableIndex.GetRange(streamHash, version, version))
                {
                    if (indexEntry.Position == rec.LogPosition) // already checked that
                        continue;

                    rec = GetEventRecord(reader, indexEntry);
                    if (rec.EventStreamId == streamId)
                    {
                        _succReadCount += 1;
                        record = rec;
                        return true;
                    }
                    _failedReadCount += 1;
                }
            }
            record = null;
            return false;
        }

        private static EventRecord GetEventRecord(ITransactionFileReader reader, IndexEntry indexEntry)
        {
            var prepare = ReadPrepareInternal(reader, indexEntry.Position);
            var eventRecord = new EventRecord(indexEntry.Version, prepare);
            return eventRecord;
        }

        private static PrepareLogRecord ReadPrepareInternal(ITransactionFileReader reader, long pos)
        {
            var result = reader.TryReadAt(pos);
            // TODO AN need to change this to account for possibly scavenged records, shouldn't throw exception,
            // TODO AN rather return meaningful result
            if (!result.Success) 
                throw new InvalidOperationException("Couldn't read record which is supposed to be in file.");
            Debug.Assert(result.LogRecord.RecordType == LogRecordType.Prepare, "Incorrect type of log record, expected Prepare record.");
            return (PrepareLogRecord)result.LogRecord;
        }

        int IReadIndex.GetLastStreamEventNumber(string streamId)
        {
            var reader = GetReader();
            try
            {
                return GetLastStreamEventNumberInternal(reader, streamId);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private int GetLastStreamEventNumberInternal(ITransactionFileReader reader, string streamId)
        {
            Ensure.NotNull(streamId, "streamId");

            var streamHash = _hasher.Hash(streamId);
            IndexEntry latestEntry;
            if (!_tableIndex.TryGetLatestEntry(streamHash, out latestEntry))
                return ExpectedVersion.NoStream;

            var prepare = ReadPrepareInternal(reader, latestEntry.Position);
            if (prepare.EventStreamId == streamId) // LUCKY!!!
                return latestEntry.Version;

            // TODO AN here lie the problem of out of memory if the stream have A LOT of events in them
            foreach (var indexEntry in _tableIndex.GetRange(streamHash, 0, int.MaxValue))
            {
                var p = ReadPrepareInternal(reader, indexEntry.Position);
                if (p.EventStreamId == streamId)
                    return indexEntry.Version; // AT LAST!!!
            }
            return ExpectedVersion.NoStream; // no such event stream
        }

        bool IReadIndex.IsStreamDeleted(string streamId)
        {
            var reader = GetReader();
            try
            {
                return IsStreamDeletedInternal(reader, streamId);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private bool IsStreamDeletedInternal(ITransactionFileReader reader, string streamId)
        {
            EventRecord record;
            return GetStreamRecord(reader, streamId, int.MaxValue, out record);
        }

        /// <summary>
        /// Returns event records in the sequence they were committed into TF.
        /// Positions is specified as pre-positions (pointer at the beginning of the record).
        /// </summary>
        IndexReadAllResult IReadIndex.ReadAllEventsForward(TFPos pos, int maxCount)
        {
            var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);

            var records = new List<CommitEventRecord>();
            var nextPos = pos;
            // in case we are at position after which there is no commit at all, in that case we have to force 
            // PreparePosition to int.MaxValue, so if you decide to read backwards from PrevPos, 
            // you will receive all prepares.
            var prevPos = new TFPos(pos.CommitPosition, int.MaxValue);
            var count = 0;
            bool firstCommit = true;
            ITransactionFileSequentialReader seqReader = GetSeqReader();
            try
            {
                long nextCommitPos = pos.CommitPosition;
                while (count < maxCount)
                {
                    seqReader.Reposition(nextCommitPos);

                    SeqReadResult result;
                    do
                    {
                        result = seqReader.TryReadNext();
                    }
                    while (result.Success && result.LogRecord.RecordType != LogRecordType.Commit); // skip until commit

                    if (!result.Success) // no more records in TF
                        break;

                    nextCommitPos = result.RecordPostPosition;

                    var commit = (CommitLogRecord)result.LogRecord;
                    if (firstCommit)
                    {
                        firstCommit = false;
                        // for backward pass we want to allow read the same commit and skip read prepares, 
                        // so we put post-position of commit and post-position of prepare as TFPos for backward pass
                        prevPos = new TFPos(result.RecordPostPosition, pos.PreparePosition);
                    }

                    seqReader.Reposition(commit.TransactionPosition);
                    while (count < maxCount)
                    {
                        result = seqReader.TryReadNext();
                        if (!result.Success) // no more records in TF
                            break;
                        // prepare with TransactionEnd could be scavenged already
                        // so we could reach the same commit record. In that case have to stop
                        if (result.LogRecord.Position >= commit.Position) 
                            break;
                        if (result.LogRecord.RecordType != LogRecordType.Prepare) 
                            continue;

                        var prepare = (PrepareLogRecord)result.LogRecord;
                        if (prepare.TransactionPosition != commit.TransactionPosition) // wrong prepare
                            continue;

                        if ((prepare.Flags & PrepareFlags.Data) != 0) // prepare with useful data
                        {
                            if (new TFPos(commit.Position, prepare.LogPosition) >= pos)
                            {
                                var eventRecord = new EventRecord(commit.EventNumber + prepare.TransactionOffset, prepare);
                                records.Add(new CommitEventRecord(eventRecord, commit.Position));
                                count++;

                                // for forward pass position is inclusive, 
                                // so we put pre-position of commit and post-position of prepare
                                nextPos = new TFPos(commit.LogPosition, result.RecordPostPosition); 
                            }
                        }

                        if ((prepare.Flags & PrepareFlags.TransactionEnd) != 0)
                            break;
                    }
                }
            }
            finally
            {
                ReturnSeqReader(seqReader);
            }
            return new IndexReadAllResult(records, maxCount, pos, nextPos, prevPos, lastCommitPosition);
        }

        /// <summary>
        /// Returns event records in the reverse sequence they were committed into TF.
        /// Positions is specified as post-positions (pointer after the end of record).
        /// </summary>
        IndexReadAllResult IReadIndex.ReadAllEventsBackward(TFPos pos, int maxCount)
        {
            var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);

            var records = new List<CommitEventRecord>();
            var nextPos = pos;
            // in case we are at position after which there is no commit at all, in that case we have to force 
            // PreparePosition to 0, so if you decide to read backwards from PrevPos, 
            // you will receive all prepares.
            var prevPos = new TFPos(pos.CommitPosition, 0);
            var count = 0;
            bool firstCommit = true;            
            ITransactionFileSequentialReader seqReader = GetSeqReader();
            try
            {
                long nextCommitPostPos = pos.CommitPosition;
                while (count < maxCount)
                {
                    seqReader.Reposition(nextCommitPostPos);
                    
                    SeqReadResult result;
                    do
                    {
                        result = seqReader.TryReadPrev();
                    }
                    while (result.Success && result.LogRecord.RecordType != LogRecordType.Commit); // skip until commit
                    
                    if (!result.Success) // no more records in TF
                        break;

                    var commitPostPos = result.RecordPostPosition;
                    nextCommitPostPos = result.RecordPrePosition;

                    var commit = (CommitLogRecord)result.LogRecord;
                    if (firstCommit)
                    {
                        firstCommit = false;
                        // for forward pass we allow read the same commit and as we have post-positions here
                        // we can put just prepare post-position as prepare pre-position for forward read
                        // so we put pre-position of commit and post-position of prepare
                        prevPos = new TFPos(commit.LogPosition, pos.PreparePosition);
                    }

                    // as we don't know exact position of the last record of transaction,
                    // we have to sequentially scan backwards, so no need to reposition
                    //seqReader.Reposition(commitLogRecord.TransactionPosition);
                    while (count < maxCount)
                    {
                        result = seqReader.TryReadPrev();
                        if (!result.Success) // no more records in TF
                            break;
                        // prepare with TransactionBegin could be scavenged already
                        // so we could reach beyond the start of transaction. In that case we have to stop.
                        if (result.LogRecord.Position < commit.TransactionPosition)
                            break;
                        if (result.LogRecord.RecordType != LogRecordType.Prepare)
                            continue;

                        var prepare = (PrepareLogRecord)result.LogRecord;
                        if (prepare.TransactionPosition != commit.TransactionPosition) // wrong prepare
                            continue;

                        if ((prepare.Flags & PrepareFlags.Data) != 0) // prepare with useful data
                        {
                            if (new TFPos(commitPostPos, result.RecordPostPosition) <= pos)
                            {
                                var eventRecord = new EventRecord(commit.EventNumber + prepare.TransactionOffset, prepare);
                                records.Add(new CommitEventRecord(eventRecord, commit.Position));
                                count++;

                                // for backward pass we allow read the same commit, but force to skip last read prepare
                                // so we put post-position of commit and pre-position of prepare
                                nextPos = new TFPos(commitPostPos, prepare.LogPosition);
                            }
                        }
                        if ((prepare.Flags & PrepareFlags.TransactionBegin) != 0)
                            break;
                    }
                }
            }
            finally
            {
                ReturnSeqReader(seqReader);
            }
            return new IndexReadAllResult(records, maxCount, pos, nextPos, prevPos, lastCommitPosition);
        }

        CommitCheckResult IReadIndex.CheckCommitStartingAt(long prepareStartPosition)
        {
            var reader = GetReader();
            try
            {
                // TODO AN: do it without exception catching
                string streamId;
                int expectedVersion;
                try
                {
                    var firstPrepare = ReadPrepareInternal(reader, prepareStartPosition);
                    streamId = firstPrepare.EventStreamId;
                    expectedVersion = firstPrepare.ExpectedVersion;
                }
                catch (InvalidOperationException)
                {
                    return new CommitCheckResult(CommitDecision.InvalidTransaction, string.Empty, -1, -1, -1);
                }

                var curVersion = GetLastStreamEventNumberInternal(reader, streamId);

                if (curVersion == EventNumber.DeletedStream)
                    return new CommitCheckResult(CommitDecision.Deleted, streamId, curVersion, -1, -1);

                // idempotency checks
                if (expectedVersion == ExpectedVersion.Any)
                {
                    var first = true;
                    int startEventNumber = -1;
                    int endEventNumber = -1;
                    foreach (var prepare in GetTransactionPrepares(prepareStartPosition))
                    {
                        Tuple<string, int> commitedInfo;
                        if (!_committedEvents.TryGetRecord(prepare.EventId, out commitedInfo)
                            || commitedInfo.Item1 != prepare.EventStreamId)
                        {
                            return first
                                ? new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1)
                                : new CommitCheckResult(CommitDecision.CorruptedIdempotency, streamId, curVersion, -1, -1);
                        }
                        if (first)
                            startEventNumber = commitedInfo.Item2;
                        endEventNumber = commitedInfo.Item2;
                        first = false;
                    }
                    return new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, startEventNumber, endEventNumber);
                }
                else if (expectedVersion < curVersion)
                {
                    var eventNumber = expectedVersion;
                    var first = true;
                    foreach (var prepare in GetTransactionPrepares(prepareStartPosition))
                    {
                        eventNumber += 1;

                        EventRecord record;
                        // TODO AN need to discriminate implicit and explicit $stream-created event
                        // TODO AN and avoid checking implicit as it has always different EventId
                        if (!GetStreamRecord(reader, streamId, eventNumber, out record) 
                            || (eventNumber > 0 && record.EventId != prepare.EventId)) 
                        {
                            return first || eventNumber == 1 // because right now $stream-created is always considered equal
                                ? new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1)
                                : new CommitCheckResult(CommitDecision.CorruptedIdempotency, streamId, curVersion, -1, -1);
                        }
                        first = false;
                    }
                    return new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, expectedVersion + 1, eventNumber);
                }
                else if (expectedVersion > curVersion)
                {
                    return new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1);
                }

                // expectedVersion == currentVersion
                return new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        int IReadIndex.GetLastTransactionOffset(long writerCheckpoint, long transactionId)
        {
            var seqReader = GetSeqReader();
            try
            {
                seqReader.Reposition(writerCheckpoint);
                SeqReadResult result;
                while ((result = seqReader.TryReadPrevNonFlushed()).Success)
                {
                    if (result.LogRecord.Position < transactionId)
                        break;
                    if (result.LogRecord.RecordType != LogRecordType.Prepare)
                        continue;
                    var prepare = (PrepareLogRecord) result.LogRecord;
                    if (prepare.TransactionPosition == transactionId)
                        return prepare.TransactionOffset;
                }
            }
            finally
            {
                ReturnSeqReader(seqReader);
            }
            return int.MinValue;
        }

        ReadIndexStats IReadIndex.GetStatistics()
        {
            return new ReadIndexStats(Interlocked.Read(ref _succReadCount), Interlocked.Read(ref _failedReadCount));
        }

        private bool GetStreamMetadataInternal(ITransactionFileReader reader,
                                               string streamId,
                                               out bool streamExists,
                                               out StreamMetadata metadata)
        {
            if (_metadataCache.TryGet(streamId, out metadata))
            {
                streamExists = true;
                return true;
            }

            if (GetStreamMetadataUncached(reader, streamId, out streamExists, out metadata))
            {
                _metadataCache.Put(streamId, metadata);
                return true;
            }

            return false;
        }

        private bool GetStreamMetadataUncached(ITransactionFileReader reader, 
                                               string streamId, 
                                               out bool streamExists, 
                                               out StreamMetadata metadata)
        {
            metadata = new StreamMetadata(null, null);
            streamExists = false;
            EventRecord record;
            if (!GetStreamRecord(reader, streamId, 0, out record))
                return false;
            streamExists = true;
            if (record.Metadata == null || record.Metadata.Length == 0)
                return false;
            try
            {
                var json = Encoding.UTF8.GetString(record.Metadata);
                var jObj = JObject.Parse(json);

                int maxAge = -1;
                int maxCount = -1;

                JToken prop;
                if (jObj.TryGetValue(SystemMetadata.MaxAge, out prop) && prop.Type == JTokenType.Integer)
                    maxAge = prop.Value<int>();
                if (jObj.TryGetValue(SystemMetadata.MaxCount, out prop) && prop.Type == JTokenType.Integer)
                    maxCount = prop.Value<int>();

                metadata = new StreamMetadata(maxCount > 0 ? maxCount : (int?) null,
                                              maxAge > 0 ? TimeSpan.FromSeconds(maxAge) : (TimeSpan?) null);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void Close()
        {
            foreach (var reader in _readers)
            {
                reader.Close();
            }
            foreach (var seqReader in _seqReaders)
            {
                seqReader.Close();
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
