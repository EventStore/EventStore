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
using EventStore.Core.Exceptions;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public enum SingleReadResult
    {
        Success,
        NotFound,
        NoStream,
        StreamDeleted
    }

    public enum RangeReadResult
    {
        Success,
        NoStream,
        StreamDeleted
    }

    public enum CommitDecision
    {
        Ok,
        WrongExpectedVersion,
        Deleted,
        Idempotent,
        CorruptedIdempotency,
        InvalidTransaction
    }

    public struct CommitCheckResult
    {
        public readonly CommitDecision Decision;
        public readonly string EventStreamId;
        public readonly int CurrentVersion;
        public readonly int StartEventNumber;
        public readonly int EndEventNumber;

        public CommitCheckResult(CommitDecision decision, 
                                 string eventStreamId, 
                                 int currentVersion, 
                                 int startEventNumber, 
                                 int endEventNumber)
        {
            Decision = decision;
            EventStreamId = eventStreamId;
            CurrentVersion = currentVersion;
            StartEventNumber = startEventNumber;
            EndEventNumber = endEventNumber;
        }
    }

    public class ReadIndex : IDisposable, IReadIndex
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<ReadIndex>();
        private static readonly EventRecord[] EmptyRecords = new EventRecord[0];

        public long LastCommitPosition { get { return Interlocked.Read(ref _lastCommitPosition); } }

        private long _succReadCount;
        private long _failedReadCount;

        private readonly IPublisher _bus;
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

        private long _persistedPrepareCheckpoint = -1;
        private long _persistedCommitCheckpoint = -1;
        private long _lastCommitPosition = -1;

        private readonly BoundedCache<Guid, Tuple<string, int>> _commitedEvents = 
            new BoundedCache<Guid, Tuple<string, int>>(int.MaxValue, 10*1024*1024, x => 16 + 4 + 2*x.Item1.Length);

        public ReadIndex(IPublisher bus,
                         Func<ITransactionFileSequentialReader> seqReaderFactory,
                         int seqReaderCount,
                         Func<ITransactionFileReader> readerFactory,
                         int readerCount,
                         ITableIndex tableIndex,
                         IHasher hasher)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(seqReaderFactory, "seqReaderFactory");
            Ensure.Positive(seqReaderCount, "seqReaderCount");
            Ensure.NotNull(readerFactory, "readerFactory");
            Ensure.Positive(readerCount, "readerCount");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(hasher, "hasher");

            _bus = bus;
            for (int i = 0; i < seqReaderCount; ++i)
            {
                _seqReaders.Push(seqReaderFactory());
            }
            for (int i = 0; i < readerCount; ++i)
            {
                _readers.Push(readerFactory());
            }

            _tableIndex = tableIndex;
            _hasher = hasher;
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

            foreach (var rdr in _readers)
            {
                rdr.Open();
            }
            foreach (var seqRdr in _seqReaders)
            {
                seqRdr.Open();
            }

            long processed = 0;
            var seqReader = GetSeqReader();
            try
            {
                seqReader.Reposition(Math.Max(0, _persistedCommitCheckpoint));

                RecordReadResult result;
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
        }

        public void Commit(CommitLogRecord commit)
        {
            bool first = true;
            int number = -1;
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
                    //Debug.Assert(number == -1);
                    number = EventNumber.DeletedStream;

                    _commitedEvents.PutRecord(prepare.EventId, Tuple.Create(eventStreamId, number), throwOnDuplicate: false);

                    if (commit.LogPosition > _persistedCommitCheckpoint 
                        || commit.LogPosition == _persistedCommitCheckpoint && prepare.LogPosition > _persistedPrepareCheckpoint)
                        addToIndex = true;
                }
                else if ((prepare.Flags & PrepareFlags.Data) != 0)
                {
                    if (prepare.ExpectedVersion == ExpectedVersion.Any)
                    {
                        if (number == -1)
                            number = commit.EventNumber - 1;
                        number = number + 1;
                    }
                    else
                    {
                        Debug.Assert(number == -1 || number == prepare.ExpectedVersion);
                        number = prepare.ExpectedVersion + 1;
                    }

                    _commitedEvents.PutRecord(prepare.EventId, Tuple.Create(eventStreamId, number), throwOnDuplicate: false);

                    if (commit.LogPosition > _persistedCommitCheckpoint
                        || commit.LogPosition == _persistedCommitCheckpoint && prepare.LogPosition > _persistedPrepareCheckpoint)
                        addToIndex = true;
                }
                // could be just empty prepares for TransactionBegin and TransactionEnd, for instance
                if (addToIndex)
                {
#if DEBUG
                    long pos;
                    if (_tableIndex.TryGetOneValue(streamHash, number, out pos))
                    {
                        EventRecord rec;
                        if (TryReadRecord(eventStreamId, number, out rec) == SingleReadResult.Success)
                        {
                            Debugger.Break();
                            throw new Exception(
                                string.Format(
                                    "Trying to add duplicate event #{0} for stream {1}(hash {2})\nCommit: {3}\nPrepare: {4}.",
                                    number,
                                    eventStreamId,
                                    streamHash,
                                    commit,
                                    prepare));
                        }
                    }
#endif
                    _tableIndex.Add(commit.LogPosition, streamHash, number, prepare.LogPosition);
                    _bus.Publish(new ReplicationMessage.EventCommited(commit.LogPosition, number, prepare));
                }
            }
        }

        private IEnumerable<PrepareLogRecord> GetTransactionPrepares(long transactionBeginPos)
        {
            var reader = GetReader();
            RecordReadResult result;
            try
            {
                result = reader.TryReadAt(transactionBeginPos);
            }
            finally
            {
                ReturnReader(reader);
            }

            if (!result.Success)
                throw new InvalidOperationException("Couldn't read record which is supposed to be in file.");
            Debug.Assert(result.LogRecord.RecordType == LogRecordType.Prepare,
                            "Incorrect type of log record, expected Prepare record.");
            
            var transactionRecord = (PrepareLogRecord) result.LogRecord;
            
            if ((transactionRecord.Flags & PrepareFlags.TransactionEnd) != 0)
            {
                yield return transactionRecord;
                yield break;
            }

            var seqReader = GetSeqReader();
            try
            {
                seqReader.Reposition(transactionBeginPos);

                while (true)
                {
                    result = seqReader.TryReadNext();
                    if (!result.Success)
                        throw new InvalidOperationException("Couldn't read record which is supposed to be in file.");

                    var prepare = result.LogRecord as PrepareLogRecord;
                    if (prepare != null && prepare.TransactionPosition == transactionBeginPos)
                    {
                        /*&& prepare.EventStreamId == transactionRecord.EventStreamId*/
                        Debug.Assert(prepare.EventStreamId == transactionRecord.EventStreamId);
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

        public SingleReadResult TryReadRecord(string eventStreamId, int version, out EventRecord record)
        {
            var reader = GetReader();
            try
            {
                return TryReadRecordInternal(reader, eventStreamId, version, out record);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private SingleReadResult TryReadRecordInternal(ITransactionFileReader reader, 
                                                       string eventStreamId,
                                                       int version, 
                                                       out EventRecord record)
        {
            Ensure.NotNull(eventStreamId, "eventStreamId");
            Ensure.Nonnegative(version, "version");

            record = null;

            if (IsStreamDeletedInternal(reader, eventStreamId))
                return SingleReadResult.StreamDeleted;

            var success = TryGetRecordInternal(reader, eventStreamId, version, out record);
            if (success)
                return SingleReadResult.Success;

            if (version == 0)
                return SingleReadResult.NoStream;

            EventRecord rec;
            return TryGetRecordInternal(reader, eventStreamId, 0, out rec)
                            ? SingleReadResult.NotFound
                            : SingleReadResult.NoStream;
        }

        public RangeReadResult TryReadEventsForward(string eventStreamId, 
                                                    int fromEventNumber, 
                                                    int maxCount, 
                                                    out EventRecord[] records)
        {
            Ensure.NotNull(eventStreamId, "eventStreamId");
            Ensure.Nonnegative(fromEventNumber, "fromEventNumber");
            Ensure.Positive(maxCount, "maxCount");

            records = EmptyRecords;
            var streamHash = _hasher.Hash(eventStreamId);

            var reader = GetReader();
            try
            {
                if (IsStreamDeletedInternal(reader, eventStreamId))
                    return RangeReadResult.StreamDeleted;

                records = _tableIndex.GetRange(streamHash, fromEventNumber, fromEventNumber + maxCount - 1)
                                     .Select(x => ReadEventRecord(reader, x))
                                     .Where(x => x.EventStreamId == eventStreamId)
                                     .Reverse()
                                     .ToArray();
                if (records.Length > 0)
                    return RangeReadResult.Success;
                if (fromEventNumber == 0)
                    return RangeReadResult.NoStream;
                EventRecord record;
                return TryGetRecordInternal(reader, eventStreamId, 0, out record) 
                               ? RangeReadResult.Success
                               : RangeReadResult.NoStream;
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        public RangeReadResult TryReadRecordsBackwards(string eventStreamId, 
                                                       int fromEventNumber, 
                                                       int maxCount, 
                                                       out EventRecord[] records)
        {
            Ensure.NotNull(eventStreamId, "eventStreamId");
            Ensure.Positive(maxCount, "maxCount");

            records = EmptyRecords;
            var streamHash = _hasher.Hash(eventStreamId);

            var reader = GetReader();
            try
            {
                if (IsStreamDeletedInternal(reader, eventStreamId))
                    return RangeReadResult.StreamDeleted;

                int endEventNumber = fromEventNumber;
                if (endEventNumber < 0)
                {
                    endEventNumber = GetLastStreamEventNumberInternal(reader, eventStreamId);
                    if (endEventNumber == -1) // optimization to reduce index lookups
                        return RangeReadResult.NoStream;
                }

                var startEventNumber = Math.Max(0, endEventNumber - maxCount + 1);

                records = _tableIndex.GetRange(streamHash, startEventNumber, endEventNumber)
                                     .Select(x => ReadEventRecord(reader, x))
                                     .Where(x => x.EventStreamId == eventStreamId)
                                     .ToArray();
                if (records.Length > 0)
                    return RangeReadResult.Success;
                if (fromEventNumber == 0) // optimization to reduce index lookups
                    return RangeReadResult.NoStream; 
                EventRecord record;
                return TryGetRecordInternal(reader, eventStreamId, 0, out record) 
                               ? RangeReadResult.Success
                               : RangeReadResult.NoStream;
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private bool TryGetRecordInternal(ITransactionFileReader reader, 
                                          string eventStreamId, 
                                          int version, 
                                          out EventRecord record)
        {
            // we assume that you already did check for stream deletion
            Ensure.NotNull(eventStreamId, "eventStreamId");
            Ensure.Nonnegative(version, "version");

            var streamHash = _hasher.Hash(eventStreamId);

            long position;
            if (_tableIndex.TryGetOneValue(streamHash, version, out position))
            {
                EventRecord rec = ReadEventRecord(reader, new IndexEntry(streamHash, version, position));
                if (rec.EventStreamId == eventStreamId)
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

                    rec = ReadEventRecord(reader, indexEntry);
                    if (rec.EventStreamId == eventStreamId)
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

        private EventRecord ReadEventRecord(ITransactionFileReader reader, IndexEntry indexEntry)
        {
            var prepare = GetPrepareInternal(reader, indexEntry.Position);
            var eventRecord = new EventRecord(indexEntry.Version, prepare);
            return eventRecord;
        }

        public PrepareLogRecord GetPrepare(long pos)
        {
            var reader = GetReader();
            try
            {
                return GetPrepareInternal(reader, pos);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private PrepareLogRecord GetPrepareInternal(ITransactionFileReader reader, long pos)
        {
            var result = reader.TryReadAt(pos);
            if (!result.Success) throw new InvalidOperationException("Couldn't read record which is supposed to be in file.");
            Debug.Assert(result.LogRecord.RecordType == LogRecordType.Prepare, "Incorrect type of log record, expected Prepare record.");
            return (PrepareLogRecord)result.LogRecord;
        }

        public int GetLastStreamEventNumber(string eventStreamId)
        {
            var reader = GetReader();
            try
            {
                return GetLastStreamEventNumberInternal(reader, eventStreamId);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private int GetLastStreamEventNumberInternal(ITransactionFileReader reader, string eventStreamId)
        {
            Ensure.NotNull(eventStreamId, "eventStreamId");

            var streamHash = _hasher.Hash(eventStreamId);
            IndexEntry latestEntry;
            if (!_tableIndex.TryGetLatestEntry(streamHash, out latestEntry))
                return ExpectedVersion.NoStream;

            var prepare = GetPrepareInternal(reader, latestEntry.Position);
            if (prepare.EventStreamId == eventStreamId) // LUCKY!!!
                return latestEntry.Version;

            // TODO AN here lie the problem of out of memory if the stream have A LOT of events in them
            foreach (var indexEntry in _tableIndex.GetRange(streamHash, 0, int.MaxValue))
            {
                var p = GetPrepareInternal(reader, indexEntry.Position);
                if (p.EventStreamId == eventStreamId)
                    return indexEntry.Version; // AT LAST!!!
            }
            return ExpectedVersion.NoStream; // no such event stream
        }

        public bool IsStreamDeleted(string eventStreamId)
        {
            var reader = GetReader();
            try
            {
                return IsStreamDeletedInternal(reader, eventStreamId);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private bool IsStreamDeletedInternal(ITransactionFileReader reader, string eventStreamId)
        {
            EventRecord record;
            return TryGetRecordInternal(reader, eventStreamId, int.MaxValue, out record);
        }

        public List<ResolvedEventRecord> ReadAllEventsForward(long commitPos, 
                                                              long preparePos, 
                                                              bool inclusivePos,
                                                              int maxCount, 
                                                              bool resolveLinks)
        {
            var records = new List<ResolvedEventRecord>();
            var count = 0;
            ITransactionFileSequentialReader seqReader = GetSeqReader();
            try
            {
                long nextCommitPos = commitPos;
                while (count < maxCount)
                {
                    seqReader.Reposition(nextCommitPos);
                    var result = seqReader.TryReadNext();
                    // skip until commit
                    while (result.Success && result.LogRecord.RecordType != LogRecordType.Commit)
                    {
                        result = seqReader.TryReadNext();
                    }

                    if (!result.Success) // no more records in TF
                        break;

                    nextCommitPos = seqReader.Position;

                    var commitLogRecord = (CommitLogRecord)result.LogRecord;
                    int nextEventNumber = commitLogRecord.EventNumber;

                    seqReader.Reposition(commitLogRecord.TransactionPosition);
                    while (count < maxCount)
                    {
                        result = seqReader.TryReadNext();

                        if (!result.Success) // no more records in TF
                            break;
                        // prepare with TransactionEnd could be scavenged already
                        // so we could reach the same commit record. In that case have to stop
                        if (result.LogRecord.Position >= commitLogRecord.Position) 
                            break;
                        if (result.LogRecord.RecordType != LogRecordType.Prepare) 
                            continue;

                        var prepareRecord = (PrepareLogRecord)result.LogRecord;
                        if (prepareRecord.TransactionPosition != commitLogRecord.TransactionPosition) // wrong prepare
                            continue;

                        if ((prepareRecord.Flags & PrepareFlags.Data) != 0) // prepare with useful data
                        {
                            if (inclusivePos ? prepareRecord.LogPosition >= preparePos : prepareRecord.LogPosition > preparePos)
                            {
                                var eventRecord = new EventRecord(nextEventNumber, prepareRecord);

                                EventRecord linkToEvent = null;
                                if (resolveLinks)
                                {
                                    var resolved = ResolveLinkToEvent(eventRecord);
                                    if (resolved != null)
                                    {
                                        linkToEvent = eventRecord;
                                        eventRecord = resolved;
                                    }
                                }

                                records.Add(new ResolvedEventRecord(eventRecord, linkToEvent, commitLogRecord.Position));
                                count++;
                            }
                            nextEventNumber++;
                        }

                        if ((prepareRecord.Flags & PrepareFlags.TransactionEnd) != 0)
                            break;
                    }
                }
            }
            finally
            {
                ReturnSeqReader(seqReader);
            }
            return records;
        }

        public List<ResolvedEventRecord> ReadAllEventsBackwards(long commitPos,
                                                                long preparePos,
                                                                bool inclusivePos,
                                                                int maxCount,
                                                                bool resolveLinks)
        {
            var records = new List<ResolvedEventRecord>();
            var count = 0;
            ITransactionFileSequentialReader seqReader = GetSeqReader();
            try
            {
                long nextCommitPos = commitPos;
                while (count < maxCount)
                {
                    seqReader.Reposition(nextCommitPos);
                    var result = seqReader.TryReadPrev();
                    // skip until commit
                    while (result.Success && result.LogRecord.RecordType != LogRecordType.Commit)
                    {
                        result = seqReader.TryReadPrev();
                    }

                    if (!result.Success) // no more records in TF
                        break;

                    nextCommitPos = seqReader.Position;

                    var commitLogRecord = (CommitLogRecord)result.LogRecord;
                    int nextEventNumber = commitLogRecord.EventNumber;

                    seqReader.Reposition(commitLogRecord.TransactionPosition);
                    while (count < maxCount)
                    {
                        result = seqReader.TryReadNext();

                        if (!result.Success) // no more records in TF
                            break;
                        // prepare with TransactionEnd could be scavenged already
                        // so we could reach the same commit record. In that case have to stop
                        if (result.LogRecord.Position >= commitLogRecord.Position)
                            break;
                        if (result.LogRecord.RecordType != LogRecordType.Prepare)
                            continue;

                        var prepareRecord = (PrepareLogRecord)result.LogRecord;
                        if (prepareRecord.TransactionPosition != commitLogRecord.TransactionPosition) // wrong prepare
                            continue;

                        if ((prepareRecord.Flags & PrepareFlags.Data) != 0) // prepare with useful data
                        {
                            if (inclusivePos ? prepareRecord.LogPosition >= preparePos : prepareRecord.LogPosition > preparePos)
                            {
                                var eventRecord = new EventRecord(nextEventNumber, prepareRecord);

                                EventRecord linkToEvent = null;
                                if (resolveLinks)
                                {
                                    var resolved = ResolveLinkToEvent(eventRecord);
                                    if (resolved != null)
                                    {
                                        linkToEvent = eventRecord;
                                        eventRecord = resolved;
                                    }
                                }

                                records.Add(new ResolvedEventRecord(eventRecord, linkToEvent, commitLogRecord.Position));
                                count++;
                            }
                            nextEventNumber++;
                        }

                        if ((prepareRecord.Flags & PrepareFlags.TransactionEnd) != 0)
                            break;
                    }
                }
            }
            finally
            {
                ReturnSeqReader(seqReader);
            }
            return records;
        }

        public EventRecord ResolveLinkToEvent(EventRecord eventRecord)
        {
            Ensure.NotNull(eventRecord, "eventRecord");
            var reader = GetReader();
            try
            {
                return ResolveLinkToEventInternal(reader, eventRecord);
            }
            finally
            {
                ReturnReader(reader);
            }
        }

        private EventRecord ResolveLinkToEventInternal(ITransactionFileReader reader, EventRecord eventRecord)
        {
            EventRecord record = null;
            if (eventRecord.EventType == SystemEventTypes.LinkTo)
            {
                bool faulted = false;
                int eventNumber = -1;
                string streamId = null;
                try
                {
                    string[] parts = Encoding.UTF8.GetString(eventRecord.Data).Split('@');
                    eventNumber = int.Parse(parts[0]);
                    streamId = parts[1];
                }
                catch (Exception exc)
                {
                    faulted = true;
                    Log.ErrorException(exc, "Error while resolving link for event record: {0}", eventRecord.ToString());
                }
                if (faulted)
                    return null;
                TryGetRecordInternal(reader, streamId, eventNumber, out record);
            }
            return record;
        }

        public string[] GetStreamIds()
        {
            const int batchSize = 100;
            var allEvents = new List<EventRecord>();
            EventRecord[] eventsBatch;

            int from = 0;
            do
            {
                var result = TryReadEventsForward(SystemStreams.StreamsStream, from, batchSize, out eventsBatch);
                if (result != RangeReadResult.Success)
                {
                    throw new SystemStreamNotFoundException(
                        string.Format("Couldn't find system stream {0}, which should've been created with projection 'Index By Streams'",
                                      SystemStreams.StreamsStream));
                }

                from += eventsBatch.Length;
                allEvents.AddRange(eventsBatch);
            }
            while (eventsBatch.Length != 0);

            var streamIds = allEvents
                .Skip(1) // streamCreated
                .Select(e =>
                {
                    var dataStr = Encoding.UTF8.GetString(e.Data);
                    var parts = dataStr.Split('@');
                    if (parts.Length < 2)
                        throw new FormatException("$streams stream event data is in bad format: {0}. Expected: eventNumber@streamid");
                    var streamid = parts[1];
                    return streamid;
                })
                .ToArray();

            return streamIds;
        }

        public CommitCheckResult CheckCommitStartingAt(long prepareStartPosition)
        {
            var reader = GetReader();
            try
            {
                // TODO AN: do it without exception catching
                string streamId;
                int expectedVersion;
                try
                {
                    var firstPrepare = GetPrepareInternal(reader, prepareStartPosition);
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
                        if (!_commitedEvents.TryGetRecord(prepare.EventId, out commitedInfo)
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
                        if (!TryGetRecordInternal(reader, streamId, eventNumber, out record) 
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

        public int GetLastTransactionOffset(long writerCheckpoint, long transactionId)
        {
            var seqReader = GetSeqReader();
            try
            {
                seqReader.Reposition(writerCheckpoint);
                RecordReadResult result;
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

        public ReadIndexStats GetStatistics()
        {
            return new ReadIndexStats(Interlocked.Read(ref _succReadCount), Interlocked.Read(ref _failedReadCount));
        }

        public void Close()
        {
            foreach (var reader in _readers)
            {
                reader.Close();
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
