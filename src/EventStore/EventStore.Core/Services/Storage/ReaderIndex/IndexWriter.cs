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
    public interface IIndexWriter
    {
        long LastCommitPosition { get; }

        long CachedTransInfo { get; }
        long NotCachedTransInfo { get; }

        void Init(long writerCheckpoint, long buildToPosition);
        void Dispose();

        void Commit(CommitLogRecord commit);
        void Commit(IList<PrepareLogRecord> commitedPrepares);
        
        CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition);
        CommitCheckResult CheckCommit(string streamId, int expectedVersion, IEnumerable<Guid> eventIds);

        void UpdateTransactionInfo(long transactionId, TransactionInfo transactionInfo);
        TransactionInfo GetTransactionInfo(long writerCheckpoint, long transactionId);
    }

    public class IndexWriter : IIndexWriter
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<IndexWriter>();

        public long LastCommitPosition { get { return Interlocked.Read(ref _lastCommitPosition); } }

        public long CachedTransInfo { get { return Interlocked.Read(ref _cachedTransInfo); } }
        public long NotCachedTransInfo { get { return Interlocked.Read(ref _notCachedTransInfo); } }

        private readonly IPublisher _bus;
        private readonly IIndexBackend _backend;
        private readonly IIndexReader _indexReader;
        private readonly ITableIndex _tableIndex;
        private readonly IHasher _hasher; 
        private readonly bool _additionalCommitChecks;

        private long _persistedPreparePos = -1;
        private long _persistedCommitPos = -1;
        private bool _indexRebuild = true;

        private long _cachedTransInfo;
        private long _notCachedTransInfo;

        private readonly ILRUCache<long, TransactionInfo> _transactionInfoCache = new LRUCache<long, TransactionInfo>(ESConsts.TransactionMetadataCacheCapacity);
        private readonly BoundedCache<Guid, Tuple<string, int>> _committedEvents = 
                new BoundedCache<Guid, Tuple<string, int>>(int.MaxValue,
                                                           ESConsts.CommitedEventsMemCacheLimit,
                                                           x => 16 + 4 + 2*x.Item1.Length + IntPtr.Size);

        private long _lastCommitPosition = -1;

        public IndexWriter(IPublisher bus, IIndexBackend backend, IIndexReader indexReader,
                           ITableIndex tableIndex, IHasher hasher, bool additionalCommitChecks)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.NotNull(backend, "backend");
            Ensure.NotNull(indexReader, "indexReader");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(hasher, "hasher");

            _bus = bus;
            _backend = backend;
            _indexReader = indexReader;
            _tableIndex = tableIndex;
            _hasher = hasher;
            _additionalCommitChecks = additionalCommitChecks;
        }

        public void Init(long writerCheckpoint, long buildToPosition)
        {
            Log.Info("TableIndex initialization...");

            _tableIndex.Initialize(writerCheckpoint);
            _persistedPreparePos = _tableIndex.PrepareCheckpoint;
            _persistedCommitPos = _tableIndex.CommitCheckpoint;
            _lastCommitPosition = _tableIndex.CommitCheckpoint;

            if (_lastCommitPosition >= writerCheckpoint)
                throw new Exception(string.Format("_lastCommitPosition {0} >= writerCheckpoint {1}", _lastCommitPosition, writerCheckpoint));

            var startTime = DateTime.UtcNow;
            var lastTime = DateTime.UtcNow;
            var reportPeriod = TimeSpan.FromSeconds(5);

            Log.Info("ReadIndex building...");

            _indexRebuild = true;
            using (var reader = _backend.BorrowReader())
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
                            if (prepare.Flags.HasAllOf(PrepareFlags.IsCommitted))
                            {
                                commitedPrepares.Add(prepare);
                                if (prepare.Flags.HasAllOf(PrepareFlags.TransactionEnd))
                                {
                                    Commit(commitedPrepares);
                                    commitedPrepares.Clear();
                                }
                            }
                            break;
                        }
                        case LogRecordType.Commit:
                            Commit((CommitLogRecord)result.LogRecord);
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

                _backend.SetSystemSettings(GetSystemSettings());
            }

            _indexRebuild = false;
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

        public void Commit(CommitLogRecord commit)
        {
            var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
            if (commit.LogPosition < lastCommitPosition || (commit.LogPosition == lastCommitPosition && !_indexRebuild))
                return;  // already committed

            string streamId = null;
            uint streamHash = 0;
            int eventNumber = int.MinValue;
            var indexEntries = new List<IndexEntry>();
            var prepares = new List<PrepareLogRecord>();

            foreach (var prepare in GetTransactionPrepares(commit.TransactionPosition, commit.LogPosition))
            {
                if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
                    continue;

                if (streamId == null)
                {
                    streamId = prepare.EventStreamId;
                    streamHash = _hasher.Hash(prepare.EventStreamId);
                }
                else
                {
                    if (prepare.EventStreamId != streamId)
                        throw new Exception(string.Format("Expected stream: {0}, actual: {1}.", streamId, prepare.EventStreamId));
                }
                eventNumber = prepare.Flags.HasAllOf(PrepareFlags.StreamDelete)
                                      ? EventNumber.DeletedStream
                                      : commit.FirstEventNumber + prepare.TransactionOffset;
                _committedEvents.PutRecord(prepare.EventId, Tuple.Create(streamId, eventNumber), throwOnDuplicate: false);

                if (new TFPos(commit.LogPosition, prepare.LogPosition) > new TFPos(_persistedCommitPos, _persistedPreparePos))
                {
                    indexEntries.Add(new IndexEntry(streamHash, eventNumber, prepare.LogPosition));
                    prepares.Add(prepare);
                }
            }

            if (indexEntries.Count > 0)
            {
                if (_additionalCommitChecks)
                {
                    CheckStreamVersion(streamId, indexEntries[0].Version, commit);
                    CheckDuplicateEvents(streamHash, commit, indexEntries, prepares);
                }
                _tableIndex.AddEntries(commit.LogPosition, indexEntries); // atomically add a whole bulk of entries
            }

            if (eventNumber != int.MinValue)
            {
                if (eventNumber < 0) throw new Exception(string.Format("EventNumber {0} is incorrect.", eventNumber));

                _backend.SetStreamLastEventNumber(streamId, eventNumber);
                if (SystemStreams.IsMetastream(streamId))
                    _backend.SetStreamMetadata(SystemStreams.OriginalStreamOf(streamId), null); // invalidate cached metadata

                if (streamId == SystemStreams.SettingsStream)
                    _backend.SetSystemSettings(GetSystemSettings(prepares[prepares.Count - 1].Data));
            }

            var newLastCommitPosition = Math.Max(commit.LogPosition, lastCommitPosition);
            if (Interlocked.CompareExchange(ref _lastCommitPosition, newLastCommitPosition, lastCommitPosition) != lastCommitPosition)
                throw new Exception("Concurrency error in ReadIndex.Commit: _lastCommitPosition was modified during Commit execution!");

            for (int i = 0, n = indexEntries.Count; i < n; ++i)
            {
                _bus.Publish(new StorageMessage.EventCommited(commit.LogPosition, new EventRecord(indexEntries[i].Version, prepares[i])));
            }
        }

        public void Commit(IList<PrepareLogRecord> commitedPrepares)
        {
            if (commitedPrepares.Count == 0)
                return;

            var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
            var lastPrepare = commitedPrepares[commitedPrepares.Count - 1];

            string streamId = lastPrepare.EventStreamId;
            uint streamHash = _hasher.Hash(streamId);
            int eventNumber = int.MinValue;
            var indexEntries = new List<IndexEntry>();
            var prepares = new List<PrepareLogRecord>();

            foreach (var prepare in commitedPrepares)
            {
                if (prepare.Flags.HasNoneOf(PrepareFlags.StreamDelete | PrepareFlags.Data))
                    continue;

                if (prepare.EventStreamId != streamId) 
                    throw new Exception(string.Format("Expected stream: {0}, actual: {1}.", streamId, prepare.EventStreamId));

                if (prepare.LogPosition < lastCommitPosition || (prepare.LogPosition == lastCommitPosition && !_indexRebuild))
                    return;  // already committed

                eventNumber = prepare.ExpectedVersion + 1; /* for committed prepare expected version is always explicit */
                _committedEvents.PutRecord(prepare.EventId, Tuple.Create(streamId, eventNumber), throwOnDuplicate: false);

                if (new TFPos(prepare.LogPosition, prepare.LogPosition) > new TFPos(_persistedCommitPos, _persistedPreparePos))
                {
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

            if (eventNumber != int.MinValue)
            {
                if (eventNumber < 0) throw new Exception(string.Format("EventNumber {0} is incorrect.", eventNumber));

                _backend.SetStreamLastEventNumber(streamId, eventNumber);
                if (SystemStreams.IsMetastream(streamId))
                    _backend.SetStreamMetadata(SystemStreams.OriginalStreamOf(streamId), null); // invalidate cached metadata

                if (streamId == SystemStreams.SettingsStream)
                    _backend.SetSystemSettings(GetSystemSettings(prepares[prepares.Count - 1].Data));
            }

            var newLastCommitPosition = Math.Max(lastPrepare.LogPosition, lastCommitPosition);
            if (Interlocked.CompareExchange(ref _lastCommitPosition, newLastCommitPosition, lastCommitPosition) != lastCommitPosition)
                throw new Exception("Concurrency error in ReadIndex.Commit: _lastCommitPosition was modified during Commit execution!");

            for (int i = 0, n = indexEntries.Count; i < n; ++i)
            {
                _bus.Publish(new StorageMessage.EventCommited(prepares[i].LogPosition, new EventRecord(indexEntries[i].Version, prepares[i])));
            }
        }

        private IEnumerable<PrepareLogRecord> GetTransactionPrepares(long transactionPos, long commitPos)
        {
            using (var reader = _backend.BorrowReader())
            {
                reader.Reposition(transactionPos);

                // in case all prepares were scavenged, we should not read past Commit LogPosition
                SeqReadResult result;
                while ((result = reader.TryReadNext()).Success && result.RecordPrePosition <= commitPos)
                {
                    if (result.LogRecord.RecordType != LogRecordType.Prepare)
                        continue;

                    var prepare = (PrepareLogRecord) result.LogRecord;
                    if (prepare.TransactionPosition != transactionPos)
                        continue;

                    yield return prepare;
                    if (prepare.Flags.HasAnyOf(PrepareFlags.TransactionEnd))
                        yield break;
                }
            }
        }

        private void CheckStreamVersion(string streamId, int newEventNumber, CommitLogRecord commit)
        {
            if (newEventNumber == EventNumber.DeletedStream)
                return;

            int lastEventNumber = _indexReader.GetLastStreamEventNumber(streamId);
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
            using (var reader = _backend.BorrowReader())
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
                            throw new Exception(string.Format(
                                "Trying to add duplicate event #{0} to stream {1} (hash {2})\nCommit: {3}\n"
                                + "Prepare: {4}\nIndexed prepare: {5}.",
                                indexEntry.Version, prepare.EventStreamId, streamHash, commit, prepare, indexedPrepare));
                    }
                }
            }
        }

        public CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition)
        {
            string streamId;
            int expectedVersion;
            using (var reader = _backend.BorrowReader())
            {
                try
                {
                    PrepareLogRecord prepare = GetPrepare(reader, transactionPosition);
                    if (prepare == null)
                    {
                        var message = string.Format("Couldn't read first prepare of to-be-commited transaction. " 
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
                    return new CommitCheckResult(CommitDecision.InvalidTransaction, string.Empty, -1, -1, -1);
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
            var curVersion = _indexReader.GetLastStreamEventNumber(streamId);
            if (curVersion == EventNumber.DeletedStream)
                return new CommitCheckResult(CommitDecision.Deleted, streamId, curVersion, -1, -1);

            // idempotency checks
            if (expectedVersion == ExpectedVersion.Any)
            {
                var first = true;
                int startEventNumber = -1;
                int endEventNumber = -1;
                foreach (var eventId in eventIds)
                {
                    Tuple<string, int> prepInfo;
                    if (!_committedEvents.TryGetRecord(eventId, out prepInfo) || prepInfo.Item1 != streamId)
                        return new CommitCheckResult(first ? CommitDecision.Ok : CommitDecision.CorruptedIdempotency,
                                                     streamId, curVersion, -1, -1);
                    if (first)
                        startEventNumber = prepInfo.Item2;
                    endEventNumber = prepInfo.Item2;
                    first = false;
                }
                return first /* no data in transaction */ 
                    ? new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1)
                    : new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, startEventNumber, endEventNumber);
            }

            if (expectedVersion < curVersion)
            {
                var eventNumber = expectedVersion;
                var first = true;
                foreach (var eventId in eventIds)
                {
                    eventNumber += 1;

                    var res = _indexReader.ReadEvent(streamId, eventNumber);
                    if (res.Result != ReadEventResult.Success || res.Record.EventId != eventId)
                        return new CommitCheckResult(first ? CommitDecision.WrongExpectedVersion : CommitDecision.CorruptedIdempotency,
                                                     streamId, curVersion, -1, -1);
                    first = false;
                }
                return first /* no data in transaction */
                    ? new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1)
                    : new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, expectedVersion + 1, eventNumber);
            }

            if (expectedVersion > curVersion)
                return new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1);

            // expectedVersion == currentVersion
            return new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1);
        }

        private SystemSettings GetSystemSettings()
        {
            var res = _indexReader.ReadEvent(SystemStreams.SettingsStream, -1);
            return res.Result == ReadEventResult.Success ? GetSystemSettings(res.Record.Data) : null;
        }

        private static SystemSettings GetSystemSettings(byte[] settingsData)
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

        public void UpdateTransactionInfo(long transactionId, TransactionInfo transactionInfo)
        {
            _transactionInfoCache.Put(transactionId, transactionInfo);
        }

        public TransactionInfo GetTransactionInfo(long writerCheckpoint, long transactionId)
        {
            TransactionInfo transactionInfo;
            if (!_transactionInfoCache.TryGet(transactionId, out transactionInfo))
            {
                if (GetTransactionInfoUncached(writerCheckpoint, transactionId, out transactionInfo))
                    _transactionInfoCache.Put(transactionId, transactionInfo);
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
            using (var reader = _backend.BorrowReader())
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
    }
}