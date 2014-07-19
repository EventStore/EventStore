using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messages;
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

    public class IndexCommitter : IIndexCommitter
    {
        public static readonly ILogger Log = LogManager.GetLoggerFor<IndexCommitter>();

        public long LastCommitPosition { get { return Interlocked.Read(ref _lastCommitPosition); } }

        private readonly IPublisher _bus;
        private readonly IIndexBackend _backend;
        private readonly IIndexReader _indexReader;
        private readonly ITableIndex _tableIndex;
        private readonly IHasher _hasher;
        private readonly bool _additionalCommitChecks;
        private long _persistedPreparePos = -1;
        private long _persistedCommitPos = -1;
        private bool _indexRebuild = true;
        private long _lastCommitPosition = -1;

        public IndexCommitter(IPublisher bus, IIndexBackend backend, IIndexReader indexReader,
                              ITableIndex tableIndex, IHasher hasher, bool additionalCommitChecks)
        {
            _bus = bus;
            _backend = backend;
            _indexReader = indexReader;
            _tableIndex = tableIndex;
            _hasher = hasher;
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
                                var prepare = (PrepareLogRecord)result.LogRecord;
                                if (prepare.Flags.HasAnyOf(PrepareFlags.IsCommitted))
                                {
                                    if (prepare.Flags.HasAnyOf(PrepareFlags.Data | PrepareFlags.StreamDelete))
                                        commitedPrepares.Add(prepare);
                                    if (prepare.Flags.HasAnyOf(PrepareFlags.TransactionEnd))
                                    {
                                        Commit(commitedPrepares, result.Eof);
                                        commitedPrepares.Clear();
                                    }
                                }
                                break;
                            }
                        case LogRecordType.Commit:
                            Commit((CommitLogRecord)result.LogRecord, result.Eof);
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
                                  processed, (result.RecordPostPosition - startPosition) * 100.0 / (buildToPosition - startPosition));
                        lastTime = DateTime.UtcNow;
                    }
                }
                Log.Debug("ReadIndex rebuilding done: total processed {0} records, time elapsed: {1}.", processed, DateTime.UtcNow - startTime);
                _bus.Publish(new StorageMessage.TfEofAtNonCommitRecord());
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

        public int Commit(CommitLogRecord commit, bool isTfEof)
        {
            int eventNumber = EventNumber.Invalid;

            var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
            if (commit.LogPosition < lastCommitPosition || (commit.LogPosition == lastCommitPosition && !_indexRebuild))
                return eventNumber;  // already committed

            string streamId = null;
            uint streamHash = 0;
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

            if (eventNumber != EventNumber.Invalid)
            {
                if (eventNumber < 0) throw new Exception(string.Format("EventNumber {0} is incorrect.", eventNumber));

                _backend.SetStreamLastEventNumber(streamId, eventNumber);
                if (SystemStreams.IsMetastream(streamId))
                    _backend.SetStreamMetadata(SystemStreams.OriginalStreamOf(streamId), null); // invalidate cached metadata

                if (streamId == SystemStreams.SettingsStream)
                    _backend.SetSystemSettings(DeserializeSystemSettings(prepares[prepares.Count - 1].Data));
            }

            var newLastCommitPosition = Math.Max(commit.LogPosition, lastCommitPosition);
            if (Interlocked.CompareExchange(ref _lastCommitPosition, newLastCommitPosition, lastCommitPosition) != lastCommitPosition)
                throw new Exception("Concurrency error in ReadIndex.Commit: _lastCommitPosition was modified during Commit execution!");

            for (int i = 0, n = indexEntries.Count; i < n; ++i)
            {
                _bus.Publish(
                    new StorageMessage.EventCommitted(
                        commit.LogPosition,
                        new EventRecord(indexEntries[i].Version, prepares[i]),
                        isTfEof && i == n - 1));
            }

            return eventNumber;
        }

        public int Commit(IList<PrepareLogRecord> commitedPrepares, bool isTfEof)
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

                if (prepare.LogPosition < lastCommitPosition || (prepare.LogPosition == lastCommitPosition && !_indexRebuild))
                    continue;  // already committed

                eventNumber = prepare.ExpectedVersion + 1; /* for committed prepare expected version is always explicit */

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

            if (eventNumber != EventNumber.Invalid)
            {
                if (eventNumber < 0) throw new Exception(string.Format("EventNumber {0} is incorrect.", eventNumber));

                _backend.SetStreamLastEventNumber(streamId, eventNumber);
                if (SystemStreams.IsMetastream(streamId))
                    _backend.SetStreamMetadata(SystemStreams.OriginalStreamOf(streamId), null); // invalidate cached metadata

                if (streamId == SystemStreams.SettingsStream)
                    _backend.SetSystemSettings(DeserializeSystemSettings(prepares[prepares.Count - 1].Data));
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
            using (var reader = _backend.BorrowReader())
            {
                reader.Reposition(transactionPos);

                // in case all prepares were scavenged, we should not read past Commit LogPosition
                SeqReadResult result;
                while ((result = reader.TryReadNext()).Success && result.RecordPrePosition <= commitPos)
                {
                    if (result.LogRecord.RecordType != LogRecordType.Prepare)
                        continue;

                    var prepare = (PrepareLogRecord)result.LogRecord;
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
            using (var reader = _backend.BorrowReader())
            {
                var entries = _tableIndex.GetRange(streamHash, indexEntries[0].Version, indexEntries[indexEntries.Count - 1].Version);
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
    }
}