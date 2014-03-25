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
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public interface IIndexCommitter
    {
        long LastCommitPosition { get; }
        void Init(long buildToPosition);
        void Dispose();
        int Commit(CommitLogRecord commit, bool isTfEof);
        int Commit(IList<PrepareLogRecord> commitedPrepares, bool isTfEof);
    }

    public interface IIndexWriter
    {
        long CachedTransInfo { get; }
        long NotCachedTransInfo { get; }

        void Reset();
        CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition);
        CommitCheckResult CheckCommit(string streamId, int expectedVersion, IEnumerable<Guid> eventIds);
        void PreCommit(CommitLogRecord commit);
        void PreCommit(IList<PrepareLogRecord> commitedPrepares);
        void UpdateTransactionInfo(long transactionId, long logPosition, TransactionInfo transactionInfo);
        TransactionInfo GetTransactionInfo(long writerCheckpoint, long transactionId);
        void PurgeNotProcessedCommitsTill(long checkpoint);
        void PurgeNotProcessedTransactions(long checkpoint);

        bool IsSoftDeleted(string streamId);
        int GetStreamLastEventNumber(string streamId);
        StreamMetadata GetStreamMetadata(string streamId);
        RawMetaInfo GetStreamRawMeta(string streamId);
    }

    public struct RawMetaInfo
    {
        public readonly int MetaLastEventNumber;
        public readonly byte[] RawMeta;

        public RawMetaInfo(int metaLastEventNumber, byte[] rawMeta)
        {
            MetaLastEventNumber = metaLastEventNumber;
            RawMeta = rawMeta;
        }
    }

    public class IndexWriter : IIndexWriter, IIndexCommitter
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<IndexWriter>();

        public long CachedTransInfo { get { return Interlocked.Read(ref _cachedTransInfo); } }
        public long NotCachedTransInfo { get { return Interlocked.Read(ref _notCachedTransInfo); } }

        private readonly IIndexCache _indexCache;
        private readonly IIndexReader _indexReader;

        private readonly IStickyLRUCache<long, TransactionInfo> _transactionInfoCache = new StickyLRUCache<long, TransactionInfo>(ESConsts.TransactionMetadataCacheCapacity);
        private readonly Queue<TransInfo> _notProcessedTrans = new Queue<TransInfo>();
        private readonly BoundedCache<Guid, EventInfo> _committedEvents = new BoundedCache<Guid, EventInfo>(int.MaxValue, ESConsts.CommitedEventsMemCacheLimit, x => 16 + 4 + IntPtr.Size + 2*x.StreamId.Length);
        private readonly IStickyLRUCache<string, int> _streamVersions = new StickyLRUCache<string, int>(ESConsts.StreamInfoCacheCapacity);
        private readonly IStickyLRUCache<string, StreamMeta> _streamRawMetas = new StickyLRUCache<string, StreamMeta>(0); // store nothing flushed, only sticky non-flushed stuff
        private readonly Queue<CommitInfo> _notProcessedCommits = new Queue<CommitInfo>();

        private long _cachedTransInfo;
        private long _notCachedTransInfo;

        
        public long LastCommitPosition { get { return Interlocked.Read(ref _lastCommitPosition); } }

        private readonly IPublisher _bus;
        private readonly ITableIndex _tableIndex;
        private readonly IHasher _hasher;
        private readonly bool _additionalCommitChecks;
        private long _persistedPreparePos = -1;
        private long _persistedCommitPos = -1;
        private long _lastCommitPosition = -1;

        public IndexWriter(IPublisher bus, ITableIndex tableIndex, IHasher hasher, IIndexCache indexCache, IIndexReader indexReader, bool additionalCommitChecks)
        {
            Ensure.NotNull(indexCache, "indexBackend");
            Ensure.NotNull(indexReader, "indexReader");
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(hasher, "hasher");
            _bus = bus;
            _tableIndex = tableIndex;
            _hasher = hasher;
            _indexCache = indexCache;
            _indexReader = indexReader;
            _additionalCommitChecks = additionalCommitChecks;
        }

        public void Init(long buildToPosition)
        {
            Log.Info("TableIndex initialization...");

            _tableIndex.Initialize(buildToPosition);
            _persistedPreparePos = _tableIndex.PrepareCheckpoint;
            _persistedCommitPos = _tableIndex.CommitCheckpoint;
            _lastCommitPosition = _tableIndex.CommitCheckpoint;

            if (_lastCommitPosition >= buildToPosition)
                throw new Exception(string.Format("_lastCommitPosition {0} >= buildToPosition {1}", _lastCommitPosition, buildToPosition));

            var startTime = DateTime.UtcNow;
            var lastTime = DateTime.UtcNow;
            var reportPeriod = TimeSpan.FromSeconds(5);

            Log.Info("ReadIndex building...");

            using (var reader = _indexCache.BorrowReader())
            {
                var startPosition = Math.Max(0, _persistedCommitPos);
                reader.Reposition(startPosition);

                var commitedPrepares = new List<PrepareLogRecord>();

                long processed = 0;
                SeqReadResult result;
                while ((result = reader.TryReadNext()).Success && result.LogRecord.LogPosition < buildToPosition)
                {
                    switch (result.LogRecord.RecordType)
                    {
                        case LogRecordType.Prepare:
                        {
                            var prepare = (PrepareLogRecord) result.LogRecord;
                            if (prepare.Flags.HasAnyOf(PrepareFlags.IsCommitted))
                            {
                                if (prepare.Flags.HasAnyOf(PrepareFlags.Data | PrepareFlags.StreamDelete))
                                    commitedPrepares.Add(prepare);
                                if (prepare.Flags.HasAnyOf(PrepareFlags.TransactionEnd))
                                {
                                    Commit(commitedPrepares, result.Eof, true, 0, false);
                                    commitedPrepares.Clear();
                                }
                            }
                            break;
                        }
                        case LogRecordType.Commit:
                        var commitRecord = (CommitLogRecord) result.LogRecord;
                            Commit(commitRecord, result.Eof, true);
                            break;
                        case LogRecordType.System:
                            break;
                        default:
                            throw new Exception(string.Format("Unknown RecordType: {0}", result.LogRecord.RecordType));
                    }

                    processed += 1;
                    if (DateTime.UtcNow - lastTime > reportPeriod || processed % 100000 == 0)
                    {
                        Log.Debug("ReadIndex Rebuilding: processed {0} records ({1:0.0}%).",
                                  processed, (result.RecordPostPosition - startPosition)*100.0/(buildToPosition - startPosition));
                        lastTime = DateTime.UtcNow;
                    }
                }
                Log.Debug("ReadIndex rebuilding done: total processed {0} records, time elapsed: {1}.", processed, DateTime.UtcNow - startTime);
                _bus.Publish(new StorageMessage.TfEofAtNonCommitRecord());
                _indexCache.SetSystemSettings(GetSystemSettings());
            }
        }

        public void Dispose()
        {
            try
            {
                _tableIndex.Close(removeFiles: false);
            }
            catch (TimeoutException exc)
            {
                Log.ErrorException(exc, "Timeout exception when trying to close TableIndex.");
                throw;
            }
        }

        public int Commit(CommitLogRecord commit, bool isTfEof) {
            return Commit(commit, isTfEof, false);
        }

        private int Commit(CommitLogRecord commit, bool isTfEof, bool doingInit)
        {            
            int eventNumber = EventNumber.Invalid;

            var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
            if (commit.LogPosition < lastCommitPosition || (commit.LogPosition == lastCommitPosition && !doingInit))
                return eventNumber;  // already committed
            var shit = GetTransactionPrepares(commit.TransactionPosition, commit.LogPosition).ToList();
            return Commit(shit, isTfEof, doingInit, commit.FirstEventNumber, true);
        }

        public int Commit(IList<PrepareLogRecord> commitedPrepares, bool isTfEof) {
            return Commit(commitedPrepares, isTfEof, false, 0, false);
        }

        private int Commit(IList<PrepareLogRecord> commitedPrepares, bool isTfEof, bool doingInit, int commitOffset, bool isCommit)
        {
            int eventNumber = EventNumber.Invalid;

            if (commitedPrepares.Count == 0)
                return eventNumber;

            var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
            var lastPrepare = commitedPrepares[commitedPrepares.Count - 1];

            string streamId = lastPrepare.EventStreamId;
            uint streamHash = _hasher.Hash(streamId);
            var indexEntries = new List<IndexEntry>();
            var prepares = new List<PrepareLogRecord>();

            foreach (var prepare in commitedPrepares)
            {
                if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
                    continue;

                if (prepare.EventStreamId != streamId) 
                    throw new Exception(string.Format("Expected stream: {0}, actual: {1}.", streamId, prepare.EventStreamId));

                if (!isCommit && (prepare.LogPosition < lastCommitPosition || (prepare.LogPosition == lastCommitPosition && !doingInit)))
                    continue;  // already committed

                if(!isCommit) {
                    eventNumber = prepare.ExpectedVersion + 1; /* for committed prepare expected version is always explicit */
                }
                else {
                    eventNumber = prepare.Flags.HasAllOf(PrepareFlags.StreamDelete)
                      ? EventNumber.DeletedStream
                      : commitOffset + prepare.TransactionOffset;
                }
                if (new TFPos(prepare.LogPosition, prepare.LogPosition) > new TFPos(_persistedCommitPos, _persistedPreparePos))
                {
                    //TODO GFY FIX THIS UP A BIT THERE ARE BETTER WAYS OF DOING THIS
                    if(doingInit) {
                        _committedEvents.PutRecord(prepare.EventId, new EventInfo(streamId, eventNumber), throwOnDuplicate: false);
                    }
                    indexEntries.Add(new IndexEntry(streamHash, eventNumber, prepare.LogPosition));
                    prepares.Add(prepare);
                }
            }

            if (indexEntries.Count > 0)
            {
                if (_additionalCommitChecks)
                {
                    CheckStreamVersion(streamId, indexEntries[0].Version, null); // TODO AN: bad passing null commit
                    CheckDuplicateEvents(streamHash, null, indexEntries, prepares); // TODO AN: bad passing null commit
                }
                _tableIndex.AddEntries(lastPrepare.LogPosition, indexEntries); // atomically add a whole bulk of entries
            }

            if (eventNumber != EventNumber.Invalid)
            {
                if (eventNumber < 0) throw new Exception(string.Format("EventNumber {0} is incorrect.", eventNumber));

                _indexCache.SetStreamLastEventNumber(streamId, eventNumber);
                if (SystemStreams.IsMetastream(streamId))
                    _indexCache.SetStreamMetadata(SystemStreams.OriginalStreamOf(streamId), null); // invalidate cached metadata

                if (streamId == SystemStreams.SettingsStream)
                    _indexCache.SetSystemSettings(DeserializeSystemSettings(prepares[prepares.Count - 1].Data));
            }

            var newLastCommitPosition = Math.Max(lastPrepare.LogPosition, lastCommitPosition);
            if (Interlocked.CompareExchange(ref _lastCommitPosition, newLastCommitPosition, lastCommitPosition) != lastCommitPosition)
                throw new Exception("Concurrency error in ReadIndex.Commit: _lastCommitPosition was modified during Commit execution!");

            for (int i = 0, n = indexEntries.Count; i < n; ++i)
            {
                _bus.Publish(
                    new StorageMessage.EventCommitted(
                        prepares[i].LogPosition,
                        new EventRecord(indexEntries[i].Version, prepares[i]),
                        isTfEof && i == n - 1));
            }

            return eventNumber;
        }

        private IEnumerable<PrepareLogRecord> GetTransactionPrepares(long transactionPos, long commitPos)
        {
            using (var reader = _indexCache.BorrowReader())
            {
                reader.Reposition(transactionPos);

                // in case all prepares were scavenged, we should not read past Commit LogPosition
                SeqReadResult result;
                while ((result = reader.TryReadNext()).Success && result.RecordPrePosition <= commitPos)
                {
                    if (result.LogRecord.RecordType != LogRecordType.Prepare)
                        continue;

                    var prepare = (PrepareLogRecord) result.LogRecord;
                    if (prepare.TransactionPosition == transactionPos)
                    {
                        yield return prepare;
                        if (prepare.Flags.HasAnyOf(PrepareFlags.TransactionEnd))
                            yield break;
                    }
                }
            }
        }

        private void CheckStreamVersion(string streamId, int newEventNumber, CommitLogRecord commit)
        {
            if (newEventNumber == EventNumber.DeletedStream)
                return;

            int lastEventNumber = _indexReader.GetStreamLastEventNumber(streamId);
            if (newEventNumber != lastEventNumber + 1)
            {
                if (Debugger.IsAttached)
                    Debugger.Break();
                else 
                    throw new Exception(
                            string.Format("Commit invariant violation: new event number {0} doesn't correspond to current stream version {1}.\n"
                                          + "Stream ID: {2}.\nCommit: {3}.", newEventNumber, lastEventNumber, streamId, commit));
            }
        }

        private void CheckDuplicateEvents(uint streamHash, CommitLogRecord commit, IList<IndexEntry> indexEntries, IList<PrepareLogRecord> prepares)
        {
            using (var reader = _indexCache.BorrowReader())
            {
                var entries = _tableIndex.GetRange(streamHash, indexEntries[0].Version, indexEntries[indexEntries.Count-1].Version);
                foreach (var indexEntry in entries)
                {
                    var prepare = prepares[indexEntry.Version - indexEntries[0].Version];
                    PrepareLogRecord indexedPrepare = GetPrepare(reader, indexEntry.Position);
                    if (indexedPrepare != null && indexedPrepare.EventStreamId == prepare.EventStreamId)
                    {
                        if (Debugger.IsAttached)
                            Debugger.Break();
                        else
                            throw new Exception(
                                    string.Format("Trying to add duplicate event #{0} to stream {1} (hash {2})\nCommit: {3}\n"
                                                  + "Prepare: {4}\nIndexed prepare: {5}.",
                                                  indexEntry.Version, prepare.EventStreamId, streamHash, commit, prepare, indexedPrepare));
                    }
                }
            }
        }

        private SystemSettings GetSystemSettings()
        {
            var res = _indexReader.ReadEvent(SystemStreams.SettingsStream, -1);
            return res.Result == ReadEventResult.Success ? DeserializeSystemSettings(res.Record.Data) : null;
        }

        private static SystemSettings DeserializeSystemSettings(byte[] settingsData)
        {
            try
            {
                return SystemSettings.FromJsonBytes(settingsData);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error deserializing SystemSettings record.");
            }
            return null;
        }

        private static PrepareLogRecord GetPrepare(TFReaderLease reader, long logPosition)
        {
            RecordReadResult result = reader.TryReadAt(logPosition);
            if (!result.Success)
                return null;
            if (result.LogRecord.RecordType != LogRecordType.Prepare)
                throw new Exception(string.Format("Incorrect type of log record {0}, expected Prepare record.",
                                                  result.LogRecord.RecordType));
            return (PrepareLogRecord)result.LogRecord;
        }

        public void Reset()
        {
            _notProcessedCommits.Clear();
            _streamVersions.Clear();
            _streamRawMetas.Clear();
            _notProcessedTrans.Clear();
            _transactionInfoCache.Clear();
        }

        public CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition)
        {
            string streamId;
            int expectedVersion;
            using (var reader = _indexCache.BorrowReader())
            {
                try
                {
                    PrepareLogRecord prepare = GetPrepare(reader, transactionPosition);
                    if (prepare == null)
                    {
                        var message = string.Format("Couldn't read first prepare of to-be-committed transaction. "
                                                    + "Transaction pos: {0}, commit pos: {1}.",
                                                    transactionPosition, commitPosition);
                        Log.Error(message);
                        throw new InvalidOperationException(message);
                    }
                    streamId = prepare.EventStreamId;
                    expectedVersion = prepare.ExpectedVersion;
                }
                catch (InvalidOperationException)
                {
                    return new CommitCheckResult(CommitDecision.InvalidTransaction, string.Empty, -1, -1, -1, false);
                }
            }

            // we should skip prepares without data, as they don't mean anything for idempotency
            // though we have to check deletes, otherwise they always will be considered idempotent :)
            var eventIds = from prepare in GetTransactionPrepares(transactionPosition, commitPosition)
                           where prepare.Flags.HasAnyOf(PrepareFlags.Data | PrepareFlags.StreamDelete)
                           select prepare.EventId;
            return CheckCommit(streamId, expectedVersion, eventIds);
        }

        public CommitCheckResult CheckCommit(string streamId, int expectedVersion, IEnumerable<Guid> eventIds)
        {
            var curVersion = GetStreamLastEventNumber(streamId);
            if (curVersion == EventNumber.DeletedStream)
                return new CommitCheckResult(CommitDecision.Deleted, streamId, curVersion, -1, -1, false);

            // idempotency checks
            if (expectedVersion == ExpectedVersion.Any)
            {
                var first = true;
                int startEventNumber = -1;
                int endEventNumber = -1;
                foreach (var eventId in eventIds)
                {
                    EventInfo prepInfo;
                    if (!_committedEvents.TryGetRecord(eventId, out prepInfo) || prepInfo.StreamId != streamId)
                        return new CommitCheckResult(first ? CommitDecision.Ok : CommitDecision.CorruptedIdempotency,
                                                     streamId, curVersion, -1, -1, first && IsSoftDeleted(streamId));
                    if (first)
                        startEventNumber = prepInfo.EventNumber;
                    endEventNumber = prepInfo.EventNumber;
                    first = false;
                }
                return first /* no data in transaction */
                    ? new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1, IsSoftDeleted(streamId))
                    : new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, startEventNumber, endEventNumber, false);
            }

            if (expectedVersion < curVersion)
            {
                var eventNumber = expectedVersion;
                foreach (var eventId in eventIds)
                {
                    eventNumber += 1;

                    EventInfo prepInfo;
                    if (_committedEvents.TryGetRecord(eventId, out prepInfo)
                        && prepInfo.StreamId == streamId
                        && prepInfo.EventNumber == eventNumber)
                        continue;

                    var res = _indexReader.ReadPrepare(streamId, eventNumber);
                    if (res != null && res.EventId == eventId)
                        continue;

                    var first = eventNumber == expectedVersion + 1;
                    if (!first)
                        return new CommitCheckResult(CommitDecision.CorruptedIdempotency, streamId, curVersion, -1, -1, false);

                    if (expectedVersion == ExpectedVersion.NoStream && IsSoftDeleted(streamId))
                        return new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1, true);

                    return new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1, false);
                }
                return eventNumber == expectedVersion /* no data in transaction */
                    ? new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1, false)
                    : new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, expectedVersion + 1, eventNumber, false);
            }

            if (expectedVersion > curVersion)
                return new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1, false);

            // expectedVersion == currentVersion
            return new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1, IsSoftDeleted(streamId));
        }

        public void PreCommit(CommitLogRecord commit)
        {
            string streamId = null;
            int eventNumber = int.MinValue;
            PrepareLogRecord lastPrepare = null;

            foreach (var prepare in GetTransactionPrepares(commit.TransactionPosition, commit.LogPosition))
            {
                if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
                    continue;

                if (streamId == null) 
                    streamId = prepare.EventStreamId;

                if (prepare.EventStreamId != streamId)
                    throw new Exception(string.Format("Expected stream: {0}, actual: {1}.", streamId, prepare.EventStreamId));

                eventNumber = prepare.Flags.HasAnyOf(PrepareFlags.StreamDelete)
                                      ? EventNumber.DeletedStream
                                      : commit.FirstEventNumber + prepare.TransactionOffset;
                lastPrepare = prepare;
            }

            if (eventNumber != int.MinValue)
                _streamVersions.Put(streamId, eventNumber, +1);

            if (lastPrepare != null && SystemStreams.IsMetastream(streamId))
            {
                var rawMeta = lastPrepare.Data;
                _streamRawMetas.Put(SystemStreams.OriginalStreamOf(streamId), new StreamMeta(rawMeta, null), +1);
            }
        }

        public void PreCommit(IList<PrepareLogRecord> commitedPrepares)
        {
            if (commitedPrepares.Count == 0)
                return;

            var lastPrepare = commitedPrepares[commitedPrepares.Count - 1];
            string streamId = lastPrepare.EventStreamId;
            int eventNumber = int.MinValue;
            foreach (var prepare in commitedPrepares)
            {
                if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
                    continue;

                if (prepare.EventStreamId != streamId)
                    throw new Exception(string.Format("Expected stream: {0}, actual: {1}.", streamId, prepare.EventStreamId));

                eventNumber = prepare.ExpectedVersion + 1; /* for committed prepare expected version is always explicit */
                _committedEvents.PutRecord(prepare.EventId, new EventInfo(streamId, eventNumber), throwOnDuplicate: false);
            }
            _notProcessedCommits.Enqueue(new CommitInfo(streamId, lastPrepare.LogPosition));
            _streamVersions.Put(streamId, eventNumber, 1);
            if (SystemStreams.IsMetastream(streamId))
            {
                var rawMeta = lastPrepare.Data;
                _streamRawMetas.Put(SystemStreams.OriginalStreamOf(streamId), new StreamMeta(rawMeta, null), +1);
            }
        }

        public void UpdateTransactionInfo(long transactionId, long logPosition, TransactionInfo transactionInfo)
        {
            _notProcessedTrans.Enqueue(new TransInfo(transactionId, logPosition));
            _transactionInfoCache.Put(transactionId, transactionInfo, +1);
        }

        public TransactionInfo GetTransactionInfo(long writerCheckpoint, long transactionId)
        {
            TransactionInfo transactionInfo;
            if (!_transactionInfoCache.TryGet(transactionId, out transactionInfo))
            {
                if (GetTransactionInfoUncached(writerCheckpoint, transactionId, out transactionInfo))
                    _transactionInfoCache.Put(transactionId, transactionInfo, 0);
                else
                    transactionInfo = new TransactionInfo(int.MinValue, null);
                Interlocked.Increment(ref _notCachedTransInfo);
            }
            else
            {
                Interlocked.Increment(ref _cachedTransInfo);
            }
            return transactionInfo;
        }

        private bool GetTransactionInfoUncached(long writerCheckpoint, long transactionId, out TransactionInfo transactionInfo)
        {
            using (var reader = _indexCache.BorrowReader())
            {
                reader.Reposition(writerCheckpoint);
                SeqReadResult result;
                while ((result = reader.TryReadPrev()).Success)
                {
                    if (result.LogRecord.LogPosition < transactionId)
                        break;
                    if (result.LogRecord.RecordType != LogRecordType.Prepare)
                        continue;
                    var prepare = (PrepareLogRecord)result.LogRecord;
                    if (prepare.TransactionPosition == transactionId)
                    {
                        transactionInfo = new TransactionInfo(prepare.TransactionOffset, prepare.EventStreamId);
                        return true;
                    }
                }
            }
            transactionInfo = new TransactionInfo(int.MinValue, null);
            return false;
        }

        public void PurgeNotProcessedCommitsTill(long checkpoint)
        {
            while (_notProcessedCommits.Count > 0 && _notProcessedCommits.Peek().LogPosition < checkpoint)
            {
                var commitInfo = _notProcessedCommits.Dequeue();
                // decrease stickiness
                _streamVersions.Put(
                    commitInfo.StreamId,
                    x =>
                    {
                        if (!Debugger.IsAttached) Debugger.Launch(); else Debugger.Break();
                        throw new Exception(string.Format("CommitInfo for stream '{0}' is not present!", x));
                    },
                    (streamId, oldVersion) => oldVersion,
                    stickiness: -1);
                if (SystemStreams.IsMetastream(commitInfo.StreamId))
                {
                    _streamRawMetas.Put(
                        SystemStreams.OriginalStreamOf(commitInfo.StreamId),
                        x =>
                        {
                            if (!Debugger.IsAttached) Debugger.Launch(); else Debugger.Break();
                            throw new Exception(string.Format("Original stream CommitInfo for meta-stream '{0}' is not present!",
                                                              SystemStreams.MetastreamOf(x)));
                        },
                        (streamId, oldVersion) => oldVersion,
                        stickiness: -1);
                }
            }
        }

        public void PurgeNotProcessedTransactions(long checkpoint)
        {
            while (_notProcessedTrans.Count > 0 && _notProcessedTrans.Peek().LogPosition < checkpoint)
            {
                var transInfo = _notProcessedTrans.Dequeue();
                // decrease stickiness
                _transactionInfoCache.Put(
                    transInfo.TransactionId,
                    x => { throw new Exception(string.Format("TransInfo for transaction ID {0} is not present!", x)); },
                    (streamId, oldTransInfo) => oldTransInfo,
                    stickiness: -1);
            }
        }

        public bool IsSoftDeleted(string streamId)
        {
            return GetStreamMetadata(streamId).TruncateBefore == EventNumber.DeletedStream;
        }

        public int GetStreamLastEventNumber(string streamId)
        {
            int lastEventNumber;
            if (_streamVersions.TryGet(streamId, out lastEventNumber))
                return lastEventNumber;
            return _indexReader.GetStreamLastEventNumber(streamId);
        }

        public StreamMetadata GetStreamMetadata(string streamId)
        {
            StreamMeta meta;
            if (_streamRawMetas.TryGet(streamId, out meta))
            {
                if (meta.Meta != null)
                    return meta.Meta;
                var m = Helper.EatException(() => StreamMetadata.FromJsonBytes(meta.RawMeta), StreamMetadata.Empty);
                _streamRawMetas.Put(streamId, new StreamMeta(meta.RawMeta, m), 0);
                return m;
            }
            return _indexReader.GetStreamMetadata(streamId);
        }

        public RawMetaInfo GetStreamRawMeta(string streamId)
        {
            var metastreamId = SystemStreams.MetastreamOf(streamId);
            var metaLastEventNumber = GetStreamLastEventNumber(metastreamId);

            StreamMeta meta;
            if (!_streamRawMetas.TryGet(streamId, out meta))
                meta = new StreamMeta(_indexReader.ReadPrepare(metastreamId, metaLastEventNumber).Data, null);

            return new RawMetaInfo(metaLastEventNumber, meta.RawMeta);
        }

        private struct StreamMeta
        {
            public readonly byte[] RawMeta;
            public readonly StreamMetadata Meta;

            public StreamMeta(byte[] rawMeta, StreamMetadata meta)
            {
                RawMeta = rawMeta;
                Meta = meta;
            }
        }

        private struct EventInfo
        {
            public readonly string StreamId;
            public readonly int EventNumber;

            public EventInfo(string streamId, int eventNumber)
            {
                StreamId = streamId;
                EventNumber = eventNumber;
            }
        }

        private struct TransInfo
        {
            public readonly long TransactionId;
            public readonly long LogPosition;

            public TransInfo(long transactionId, long logPosition)
            {
                TransactionId = transactionId;
                LogPosition = logPosition;
            }
        }

        private struct CommitInfo
        {
            public readonly string StreamId;
            public readonly long LogPosition;

            public CommitInfo(string streamId, long logPosition)
            {
                StreamId = streamId;
                LogPosition = logPosition;
            }
        }
    }
}