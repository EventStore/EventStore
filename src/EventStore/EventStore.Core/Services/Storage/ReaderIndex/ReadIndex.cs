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
using System.Security.Principal;
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
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public class ReadIndex : IDisposable, IReadIndex
    {
        internal static readonly EventRecord[] EmptyRecords = new EventRecord[0];
        private static readonly ILogger Log = LogManager.GetLoggerFor<ReadIndex>();

        public long LastCommitPosition { get { return Interlocked.Read(ref _lastCommitPosition); } }

        private long _hashCollisions;
        private long _cachedStreamInfo;
        private long _notCachedStreamInfo;
        private long _cachedTransInfo;
        private long _notCachedTransInfo;

        private readonly ObjectPool<ITransactionFileReader> _readers;

        private readonly ITableIndex _tableIndex;
        private readonly IHasher _hasher;
        private readonly IPublisher _bus;
        private readonly ILRUCache<string, StreamCacheInfo> _streamInfoCache;
        private readonly ILRUCache<long, TransactionInfo> _transactionInfoCache = new LRUCache<long, TransactionInfo>(ESConsts.TransactionMetadataCacheCapacity); 

        private long _persistedPrepareCheckpoint = -1;
        private long _persistedCommitCheckpoint = -1;
        private long _lastCommitPosition = -1;
        private bool _indexRebuild = true;

        private readonly BoundedCache<Guid, Tuple<string, int>> _committedEvents = 
            new BoundedCache<Guid, Tuple<string, int>>(int.MaxValue, ESConsts.CommitedEventsMemCacheLimit, x => 16 + 4 + 2*x.Item1.Length + IntPtr.Size);

        private readonly bool _additionalCommitChecks;
        private readonly StreamMetadata _metastreamMetadata;

        public ReadIndex(IPublisher bus,
                         int initialReaderCount,
                         int maxReaderCount,
                         Func<ITransactionFileReader> readerFactory,
                         ITableIndex tableIndex,
                         IHasher hasher,
                         ILRUCache<string, StreamCacheInfo> streamInfoCache,
                         bool additionalCommitChecks,
                         int metastreamMaxCount)
        {
            Ensure.NotNull(bus, "bus");
            Ensure.Positive(initialReaderCount, "initialReaderCount");
            Ensure.Positive(maxReaderCount, "maxReaderCount");
            if (initialReaderCount > maxReaderCount)
                throw new ArgumentOutOfRangeException("initialReaderCount", "initialReaderCount is greater than maxReaderCount.");
            Ensure.NotNull(readerFactory, "readerFactory");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(hasher, "hasher");
            Ensure.NotNull(streamInfoCache, "streamInfoCache");
            Ensure.Positive(metastreamMaxCount, "metastreamMaxCount");

            _tableIndex = tableIndex;
            _hasher = hasher;
            _bus = bus;
            _streamInfoCache = streamInfoCache;

            _readers = new ObjectPool<ITransactionFileReader>("ReadIndex readers pool", initialReaderCount, maxReaderCount, readerFactory);

            _additionalCommitChecks = additionalCommitChecks;
            _metastreamMetadata = new StreamMetadata(metastreamMaxCount, null, null, null);
        }

        public void Init(long writerCheckpoint, long buildToPosition)
        {
            Log.Info("TableIndex initialization...");

            _tableIndex.Initialize(writerCheckpoint);
            _persistedPrepareCheckpoint = _tableIndex.PrepareCheckpoint;
            _persistedCommitCheckpoint = _tableIndex.CommitCheckpoint;
            _lastCommitPosition = _tableIndex.CommitCheckpoint;

            if (_lastCommitPosition >= writerCheckpoint)
                throw new Exception(string.Format("_lastCommitPosition {0} >= writerCheckpoint {1}", _lastCommitPosition, writerCheckpoint));

            var startTime = DateTime.UtcNow;
            var lastTime = DateTime.UtcNow;
            var reportPeriod = TimeSpan.FromSeconds(10);

            Log.Info("ReadIndex building...");

            _indexRebuild = true;
            var seqReader = _readers.Get();
            try
            {
                var startPosition = Math.Max(0, _persistedCommitCheckpoint);
                seqReader.Reposition(startPosition);

                long processed = 0;
                SeqReadResult result;
                while ((result = seqReader.TryReadNext()).Success && result.LogRecord.Position < buildToPosition)
                {
                    switch (result.LogRecord.RecordType)
                    {
                        case LogRecordType.Prepare:
                            break;
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
                                  processed,
                                  (result.RecordPostPosition - startPosition)*100.0/(buildToPosition - startPosition));
                        lastTime = DateTime.UtcNow;
                    }
                }
                Log.Debug("ReadIndex Rebuilding Done: total processed {0} records, time elapsed: {1}.", processed, DateTime.UtcNow - startTime);
            }
            finally
            {
                _readers.Return(seqReader);
            }

            _indexRebuild = false;
        }

        public void Commit(CommitLogRecord commit)
        {
            var lastCommitPosition = Interlocked.Read(ref _lastCommitPosition);
            if (commit.LogPosition < lastCommitPosition || (commit.LogPosition == lastCommitPosition && !_indexRebuild))
                return;  // already committed

            uint streamHash = 0;
            string streamId = null;
            int eventNumber = int.MinValue;
            var indexEntries = new List<IndexEntry>();
            var prepares = new List<PrepareLogRecord>();

            foreach (var prepare in GetTransactionPrepares(commit.TransactionPosition, commit.LogPosition))
            {
                if ((prepare.Flags & (PrepareFlags.StreamDelete | PrepareFlags.Data)) == 0)
                    continue;

                if (streamId == null)
                {
                    streamId = prepare.EventStreamId;
                    streamHash = _hasher.Hash(prepare.EventStreamId);
                }
                else
                    Debug.Assert(prepare.EventStreamId == streamId);

                eventNumber = (prepare.Flags & PrepareFlags.StreamDelete) != 0
                                  ? EventNumber.DeletedStream
                                  : commit.FirstEventNumber + prepare.TransactionOffset;
                _committedEvents.PutRecord(prepare.EventId, Tuple.Create(streamId, eventNumber), throwOnDuplicate: false);
                
                var addToIndex = commit.LogPosition > _persistedCommitCheckpoint
                                 || commit.LogPosition == _persistedCommitCheckpoint && prepare.LogPosition > _persistedPrepareCheckpoint;
                if (addToIndex)
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
                
                _streamInfoCache.Put(streamId,
                                     key => new StreamCacheInfo(eventNumber, null),
                                     (key, old) => new StreamCacheInfo(eventNumber, old.Metadata));
                if (SystemStreams.IsMetastream(streamId))
                {
                    // if we are committing to metastream, we need to invalidate metastream cache
                    // TODO AN: race condition in setting/clearing metadata
                    // in the meantime GetStreamMetadataCached could be trying to set stale metadata
                    _streamInfoCache.Put(SystemStreams.OriginalStreamOf(streamId),
                                         key => new StreamCacheInfo(-1, null),
                                         (key, old) => new StreamCacheInfo(old.LastEventNumber, null));
                }
            }

            var newLastCommitPosition = commit.LogPosition > lastCommitPosition ? commit.LogPosition : lastCommitPosition;
            if (Interlocked.CompareExchange(ref _lastCommitPosition, newLastCommitPosition, lastCommitPosition) != lastCommitPosition)
                throw new Exception("Concurrency error in ReadIndex.Commit: _lastCommitPosition was modified during Commit execution!");

            for (int i = 0, n = indexEntries.Count; i < n; ++i)
            {
                _bus.Publish(new StorageMessage.EventCommited(commit.LogPosition, new EventRecord(indexEntries[i].Version, prepares[i])));
            }
        }

        private void CheckStreamVersion(string streamId, int newEventNumber, CommitLogRecord commit)
        {
            if (newEventNumber == EventNumber.DeletedStream)
                return;

            var reader = _readers.Get();
            try
            {
                int lastEventNumber = GetLastStreamEventNumberUncached(reader, streamId);
                if (newEventNumber != lastEventNumber + 1)
                {
                    if (Debugger.IsAttached)
                        Debugger.Break();
                    else 
                        throw new Exception(string.Format(
                            "Commit invariant violation: new event number {0} doesn't correspond to current stream version {1}.\n"
                            + "Stream ID: {2}.\nCommit: {3}.",
                            newEventNumber, lastEventNumber, streamId, commit));
                }
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        private void CheckDuplicateEvents(uint streamHash, CommitLogRecord commit, IList<IndexEntry> indexEntries, IList<PrepareLogRecord> prepares)
        {
            var reader = _readers.Get();
            try
            {
                foreach (var indexEntry in _tableIndex.GetRange(streamHash, indexEntries[0].Version, indexEntries[indexEntries.Count-1].Version))
                {
                    var res = GetEventRecord(reader, indexEntry);
                    var prepare = prepares[indexEntry.Version - indexEntries[0].Version];
                    if (res.Success && res.Record.EventStreamId == prepare.EventStreamId)
                    {
                        if (Debugger.IsAttached)
                            Debugger.Break();
                        else
                        {
                            throw new Exception(string.Format(
                                "Trying to add duplicate event #{0} to stream {1} (hash {2})\nCommit: {3}\nPrepare: {4}\nPresent record: {5}.",
                                indexEntry.Version, prepare.EventStreamId, streamHash, commit, prepare, res.Record));
                        }
                    }
                }
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        private IEnumerable<PrepareLogRecord> GetTransactionPrepares(long transactionPos, long commitPos)
        {
            var seqReader = _readers.Get();
            try
            {
                seqReader.Reposition(transactionPos);

                // in case all prepares were scavenged, we should not read past Commit LogPosition
                SeqReadResult result;
                while ((result = seqReader.TryReadNext()).Success && result.RecordPrePosition <= commitPos)
                {
                    if (result.LogRecord.RecordType != LogRecordType.Prepare)
                        continue;

                    var prepare = (PrepareLogRecord) result.LogRecord;
                    if (prepare.TransactionPosition != transactionPos)
                        continue;

                    yield return prepare;
                    if ((prepare.Flags & PrepareFlags.TransactionEnd) != 0)
                        yield break;
                }
            }
            finally
            {
                _readers.Return(seqReader);
            }
        }

        IndexReadEventResult IReadIndex.ReadEvent(string streamId, int eventNumber)
        {
            var reader = _readers.Get();
            try
            {
                return ReadEventInternal(reader, streamId, eventNumber);
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        private IndexReadEventResult ReadEventInternal(ITransactionFileReader reader, string streamId, int eventNumber)
        {
            Ensure.NotNull(streamId, "streamId");
            if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");

            var lastEventNumber = GetLastStreamEventNumberCached(reader, streamId);
            if (lastEventNumber == EventNumber.DeletedStream)
                return new IndexReadEventResult(ReadEventResult.StreamDeleted);
            if (lastEventNumber == ExpectedVersion.NoStream)
                return new IndexReadEventResult(ReadEventResult.NoStream);

            if (eventNumber == -1) 
                eventNumber = lastEventNumber;

            var metadata = GetStreamMetadataCached(reader, streamId);
            if (metadata.MaxCount.HasValue)
            {
                var minEventNumber = lastEventNumber - metadata.MaxCount.Value + 1;
                if (eventNumber < minEventNumber || eventNumber > lastEventNumber)
                    return new IndexReadEventResult(ReadEventResult.NotFound);
            }

            EventRecord record;
            var success = GetStreamRecord(reader, streamId, eventNumber, out record);
            if (success)
            {
                if (metadata.MaxAge.HasValue && record.TimeStamp < DateTime.UtcNow - metadata.MaxAge.Value)
                    return new IndexReadEventResult(ReadEventResult.NotFound);
                return new IndexReadEventResult(ReadEventResult.Success, record);
            }

            return new IndexReadEventResult(ReadEventResult.NotFound);
        }

        IndexReadStreamResult IReadIndex.ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount)
        {
            Ensure.NotNull(streamId, "streamId");
            Ensure.Nonnegative(fromEventNumber, "fromEventNumber");
            Ensure.Positive(maxCount, "maxCount");

            var streamHash = _hasher.Hash(streamId);
            var reader = _readers.Get();
            try
            {
                var lastEventNumber = GetLastStreamEventNumberCached(reader, streamId);
                if (lastEventNumber == EventNumber.DeletedStream)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.StreamDeleted);
                if (lastEventNumber == ExpectedVersion.NoStream)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.NoStream);

                int startEventNumber = fromEventNumber;
                int endEventNumber = (int) Math.Min(int.MaxValue, (long) fromEventNumber + maxCount - 1);

                var metadata = GetStreamMetadataCached(reader, streamId);
                if (metadata.MaxCount.HasValue)
                {
                    var minEventNumber = lastEventNumber - metadata.MaxCount.Value + 1;
                    if (endEventNumber < minEventNumber)
                        return new IndexReadStreamResult(fromEventNumber, maxCount, EmptyRecords, metadata,
                                                         minEventNumber, lastEventNumber, isEndOfStream: false);
                    startEventNumber = Math.Max(startEventNumber, minEventNumber);
                }

                var recordsQuery = _tableIndex.GetRange(streamHash, startEventNumber, endEventNumber)
                                              .Select(x => GetEventRecord(reader, x))
                                              .Where(x => x.Success && x.Record.EventStreamId == streamId)
                                              .Select(x => x.Record);

                if (metadata.MaxAge.HasValue)
                {
                    var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
                    recordsQuery = recordsQuery.Where(x => x.TimeStamp >= ageThreshold);
                }

                var records = recordsQuery.Reverse().ToArray();
                
                int nextEventNumber = Math.Min(endEventNumber + 1, lastEventNumber + 1);
                if (records.Length > 0)
                    nextEventNumber = records[records.Length - 1].EventNumber + 1;
                var isEndOfStream = endEventNumber >= lastEventNumber;
                return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
                                                 nextEventNumber, lastEventNumber, isEndOfStream);
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        IndexReadStreamResult IReadIndex.ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount)
        {
            Ensure.NotNull(streamId, "streamId");
            Ensure.Positive(maxCount, "maxCount");

            var streamHash = _hasher.Hash(streamId);
            var reader = _readers.Get();
            try
            {
                var lastEventNumber = GetLastStreamEventNumberCached(reader, streamId);
                if (lastEventNumber == EventNumber.DeletedStream)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.StreamDeleted);
                if (lastEventNumber == ExpectedVersion.NoStream)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.NoStream);

                int endEventNumber = fromEventNumber < 0 ? lastEventNumber : fromEventNumber;
                int startEventNumber = (int)Math.Max(0L, (long)endEventNumber - maxCount + 1);
                bool isEndOfStream = false;

                var metadata = GetStreamMetadataCached(reader, streamId);
                if (metadata.MaxCount.HasValue)
                {
                    var minEventNumber = lastEventNumber - metadata.MaxCount.Value + 1;
                    if (endEventNumber < minEventNumber)
                        return new IndexReadStreamResult(fromEventNumber, maxCount, EmptyRecords, metadata,
                                                         -1, lastEventNumber, isEndOfStream: true);

                    if (startEventNumber <= minEventNumber)
                    {
                        isEndOfStream = true;
                        startEventNumber = minEventNumber;
                    }
                }

                var recordsQuery = _tableIndex.GetRange(streamHash, startEventNumber, endEventNumber)
                                              .Select(x => GetEventRecord(reader, x))
                                              .Where(x => x.Success && x.Record.EventStreamId == streamId)
                                              .Select(x => x.Record);

                if (metadata.MaxAge.HasValue)
                {
                    var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
                    recordsQuery = recordsQuery.Where(x => x.TimeStamp >= ageThreshold);
                }

                var records = recordsQuery.ToArray();

                isEndOfStream = isEndOfStream 
                                || startEventNumber == 0 
                                || startEventNumber <= lastEventNumber 
                                   && (records.Length == 0 || records[records.Length - 1].EventNumber != startEventNumber);
                int nextEventNumber = isEndOfStream ? -1 : Math.Min(startEventNumber - 1, lastEventNumber);
                return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
                                                 nextEventNumber, lastEventNumber, isEndOfStream);
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        private bool GetStreamRecord(ITransactionFileReader reader, string streamId, int version, out EventRecord record)
        {
            // we assume that you already did check for stream deletion
            Ensure.NotNullOrEmpty(streamId, "streamId");
            Ensure.Nonnegative(version, "eventNumber");

            var streamHash = _hasher.Hash(streamId);

            long position;
            if (_tableIndex.TryGetOneValue(streamHash, version, out position))
            {
                var res = GetEventRecord(reader, new IndexEntry(streamHash, version, position));
                if (res.Success && res.Record.EventStreamId == streamId)
                {
                    record = res.Record;
                    return true;
                }

                foreach (var indexEntry in _tableIndex.GetRange(streamHash, version, version))
                {
                    Interlocked.Increment(ref _hashCollisions);
                    if (indexEntry.Position == position) // already checked that
                        continue;
                    res = GetEventRecord(reader, indexEntry);
                    if (res.Success && res.Record.EventStreamId == streamId)
                    {
                        record = res.Record;
                        return true;
                    }
                }
            }
            record = null;
            return false;
        }

        private static EventResult GetEventRecord(ITransactionFileReader reader, IndexEntry indexEntry)
        {
            var res = ReadPrepareInternal(reader, indexEntry.Position);
            if (!res.Success)
                return new EventResult(false, null);
            var eventRecord = new EventRecord(indexEntry.Version, res.Record);
            return new EventResult(true, eventRecord);
        }

        private static PrepareResult ReadPrepareInternal(ITransactionFileReader reader, long pos)
        {
            RecordReadResult result = reader.TryReadAt(pos);
            if (!result.Success)
                return new PrepareResult(false, null);
            Debug.Assert(result.LogRecord.RecordType == LogRecordType.Prepare, "Incorrect type of log record, expected Prepare record.");
            return new PrepareResult(true, (PrepareLogRecord)result.LogRecord);
        }

        bool IReadIndex.IsStreamDeleted(string streamId)
        {
            return ((IReadIndex) this).GetLastStreamEventNumber(streamId) == EventNumber.DeletedStream;
        }

        int IReadIndex.GetLastStreamEventNumber(string streamId)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");

            var reader = _readers.Get();
            try
            {
                return GetLastStreamEventNumberCached(reader, streamId);
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        StreamAccessResult IReadIndex.CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user)
        {
            var reader = _readers.Get();
            try
            {
                return CheckStreamAccessInternal(reader, streamId, streamAccessType, user);
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        private StreamAccessResult CheckStreamAccessInternal(ITransactionFileReader reader, string streamId, 
                                                             StreamAccessType streamAccessType, IPrincipal user)
        {
            if (SystemStreams.IsMetastream(streamId))
            {
                switch (streamAccessType)
                {
                    case StreamAccessType.Read:
                        return CheckStreamAccessInternal(reader, SystemStreams.OriginalStreamOf(streamId), StreamAccessType.MetaRead, user);
                    case StreamAccessType.Write:
                        return CheckStreamAccessInternal(reader, SystemStreams.OriginalStreamOf(streamId), StreamAccessType.MetaWrite, user);
                    case StreamAccessType.Delete:
                    case StreamAccessType.MetaRead:
                    case StreamAccessType.MetaWrite:
                        return StreamAccessResult.Denied;
                    default:
                        throw new ArgumentOutOfRangeException("streamAccessType");
                }
            }

            if (user != null && user.IsInRole(SystemUserGroups.Admins))
                return StreamAccessResult.Granted;

            var meta = GetStreamMetadataCached(reader, streamId);
            var isSystemStream = SystemStreams.IsSystemStream(streamId);
            switch (streamAccessType)
            {
                case StreamAccessType.Read: 
                    return CheckRoleAccess(meta.Acl == null ? null : meta.Acl.ReadRole, user, isSystemStream && streamId != SystemStreams.AllStream);
                case StreamAccessType.Write:
                    return CheckRoleAccess(meta.Acl == null ? null : meta.Acl.WriteRole, user, isSystemStream);
                case StreamAccessType.Delete:
                    return CheckRoleAccess(meta.Acl == null ? null : meta.Acl.DeleteRole, user, isSystemStream);
                case StreamAccessType.MetaRead:
                    return CheckRoleAccess(meta.Acl == null ? null : meta.Acl.MetaReadRole, user, isSystemStream && streamId != SystemStreams.AllStream);
                case StreamAccessType.MetaWrite:
                    return CheckRoleAccess(meta.Acl == null ? null : meta.Acl.MetaWriteRole, user, isSystemStream);
                default: throw new ArgumentOutOfRangeException("streamAccessType");
            }
        }

        private StreamAccessResult CheckRoleAccess(string role, IPrincipal user, bool isSystemStream)
        {
            if (role == SystemUserGroups.All)
                return StreamAccessResult.Granted;
            if (role == null)
                return isSystemStream ? StreamAccessResult.Denied : StreamAccessResult.Granted;
            return (user != null && user.IsInRole(role)) ? StreamAccessResult.Granted : StreamAccessResult.Denied;
        }

        private int GetLastStreamEventNumberCached(ITransactionFileReader reader, string streamId)
        {
            Ensure.NotNull(streamId, "streamId");

            // if this is metastream -- check if original stream was deleted, if yes -- metastream is deleted as well
            if (SystemStreams.IsMetastream(streamId) 
                && GetLastStreamEventNumberCached(reader, SystemStreams.OriginalStreamOf(streamId)) == EventNumber.DeletedStream)
                return EventNumber.DeletedStream;

            StreamCacheInfo streamCacheInfo;
            if (_streamInfoCache.TryGet(streamId, out streamCacheInfo) && streamCacheInfo.LastEventNumber.HasValue)
            {
                Interlocked.Increment(ref _cachedStreamInfo);
                return streamCacheInfo.LastEventNumber.Value;
            }

            var lastEventNumber = GetLastStreamEventNumberUncached(reader, streamId);
            if (lastEventNumber != ExpectedVersion.NoStream)
            {
                // we should take Max on LastEventNumber because there could be a commit happening in parallel thread
                // so we should not overwrite the actual LastEventNumber updated by Commit method with our stale one
                _streamInfoCache.Put(
                    streamId,
                    key => new StreamCacheInfo(lastEventNumber, null),
                    (key, old) => new StreamCacheInfo(Math.Max(lastEventNumber, old.LastEventNumber ?? -1), old.Metadata));
            }
            return lastEventNumber;
        }

        private int GetLastStreamEventNumberUncached(ITransactionFileReader reader, string streamId)
        {
            Interlocked.Increment(ref _notCachedStreamInfo);

            var streamHash = _hasher.Hash(streamId);
            IndexEntry latestEntry;
            if (!_tableIndex.TryGetLatestEntry(streamHash, out latestEntry))
                return ExpectedVersion.NoStream;

            var res = ReadPrepareInternal(reader, latestEntry.Position);
            if (!res.Success)
                throw new Exception("Couldn't read latest stream's prepare! That shouldn't happen EVER!");
            if (res.Record.EventStreamId == streamId) // LUCKY!!!
                return latestEntry.Version;

            // TODO AN here lies the problem of out of memory if the stream has A LOT of events in them
            foreach (var indexEntry in _tableIndex.GetRange(streamHash, 0, int.MaxValue))
            {
                var r = ReadPrepareInternal(reader, indexEntry.Position);
                if (r.Success && r.Record.EventStreamId == streamId)
                    return indexEntry.Version; // AT LAST!!!
                Interlocked.Increment(ref _hashCollisions);
            }
            return ExpectedVersion.NoStream; // no such event stream
        }

        StreamMetadata IReadIndex.GetStreamMetadata(string streamId)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");

            var reader = _readers.Get();
            try
            {
                return GetStreamMetadataCached(reader, streamId);
            }
            finally
            {
                _readers.Return(reader);
            }
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
            // PreparePosition to long.MaxValue, so if you decide to read backwards from PrevPos, 
            // you will receive all prepares.
            var prevPos = new TFPos(pos.CommitPosition, long.MaxValue);
            long count = 0;
            bool firstCommit = true;
            ITransactionFileReader reader = _readers.Get();
            try
            {
                long nextCommitPos = pos.CommitPosition;
                while (count < maxCount)
                {
                    reader.Reposition(nextCommitPos);

                    SeqReadResult result;
                    do
                    {
                        result = reader.TryReadNext();
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

                    reader.Reposition(commit.TransactionPosition);
                    while (count < maxCount)
                    {
                        result = reader.TryReadNext();
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

                        // prepare with useful data or delete tombstone
                        if ((prepare.Flags & (PrepareFlags.Data | PrepareFlags.StreamDelete)) != 0) 
                        {
                            if (new TFPos(commit.Position, prepare.LogPosition) >= pos)
                            {
                                var eventRecord = new EventRecord(commit.FirstEventNumber + prepare.TransactionOffset, prepare);
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
                var metadata = GetStreamMetadataCached(reader, SystemStreams.AllStream);
                return new IndexReadAllResult(records, metadata, maxCount, pos, nextPos, prevPos, lastCommitPosition);
            }
            finally
            {
                _readers.Return(reader);
            }
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
            long count = 0;
            bool firstCommit = true;            
            ITransactionFileReader reader = _readers.Get();
            try
            {
                long nextCommitPostPos = pos.CommitPosition;
                while (count < maxCount)
                {
                    reader.Reposition(nextCommitPostPos);
                    
                    SeqReadResult result;
                    do
                    {
                        result = reader.TryReadPrev();
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
                        result = reader.TryReadPrev();
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

                        // prepare with useful data or delete tombstone
                        if ((prepare.Flags & (PrepareFlags.Data | PrepareFlags.StreamDelete)) != 0) 
                        {
                            if (new TFPos(commitPostPos, result.RecordPostPosition) <= pos)
                            {
                                var eventRecord = new EventRecord(commit.FirstEventNumber + prepare.TransactionOffset, prepare);
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
                var metadata = GetStreamMetadataCached(reader, SystemStreams.AllStream);
                return new IndexReadAllResult(records, metadata, maxCount, pos, nextPos, prevPos, lastCommitPosition);
            }
            finally
            {
                _readers.Return(reader);
            }
        }

        CommitCheckResult IReadIndex.CheckCommitStartingAt(long transactionPosition, long commitPosition)
        {
            var reader = _readers.Get();
            try
            {
                string streamId;
                int expectedVersion;
                try
                {
                    var firstPrepareRes = ReadPrepareInternal(reader, transactionPosition);
                    if (!firstPrepareRes.Success)
                    {
                        var message = string.Format("Couldn't read first prepare of to-be-commited transaction. " 
                                                    + "Transaction pos: {0}, commit pos: {1}.",
                                                    transactionPosition, commitPosition);
                        Log.Error(message);
                        throw new InvalidOperationException(message);
                    }
                    streamId = firstPrepareRes.Record.EventStreamId;
                    expectedVersion = firstPrepareRes.Record.ExpectedVersion;
                }
                catch (InvalidOperationException)
                {
                    return new CommitCheckResult(CommitDecision.InvalidTransaction, string.Empty, -1, -1, -1);
                }

                var curVersion = GetLastStreamEventNumberCached(reader, streamId);
                if (curVersion == EventNumber.DeletedStream)
                    return new CommitCheckResult(CommitDecision.Deleted, streamId, curVersion, -1, -1);

                // idempotency checks
                if (expectedVersion == ExpectedVersion.Any)
                {
                    var first = true;
                    int startEventNumber = -1;
                    int endEventNumber = -1;
                    foreach (var prepare in GetTransactionPrepares(transactionPosition, commitPosition))
                    {
                        // we should skip prepares without data, as they don't mean anything for idempotency
                        // though we have to check deletes, otherwise they always will be considered idempotent :)
                        if ((prepare.Flags & PrepareFlags.Data) == 0 && (prepare.Flags & PrepareFlags.StreamDelete) == 0) 
                            continue;

                        Tuple<string, int> prepInfo;
                        if (!_committedEvents.TryGetRecord(prepare.EventId, out prepInfo) || prepInfo.Item1 != prepare.EventStreamId)
                        {
                            return new CommitCheckResult(
                                first ? CommitDecision.Ok : CommitDecision.CorruptedIdempotency,
                                streamId, curVersion, -1, -1);
                        }
                        if (first)
                            startEventNumber = prepInfo.Item2;
                        endEventNumber = prepInfo.Item2;
                        first = false;
                    }
                    return first /* no data in transaction */ 
                        ? new CommitCheckResult(CommitDecision.Ok, streamId, curVersion, -1, -1)
                        : new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, startEventNumber, endEventNumber);
                }
                else if (expectedVersion < curVersion)
                {
                    var eventNumber = expectedVersion;
                    var first = true;
                    foreach (var prepare in GetTransactionPrepares(transactionPosition, commitPosition))
                    {
                        // we should skip prepares without data, as they don't mean anything for idempotency
                        // though we have to check deletes, otherwise they always will be considered idempotent :)
                        if ((prepare.Flags & PrepareFlags.Data) == 0 && (prepare.Flags & PrepareFlags.StreamDelete) == 0)
                            continue;

                        eventNumber += 1;

                        EventRecord record;
                        if (!GetStreamRecord(reader, streamId, eventNumber, out record) || record.EventId != prepare.EventId)
                        {
                            return new CommitCheckResult(
                                first ? CommitDecision.WrongExpectedVersion : CommitDecision.CorruptedIdempotency,
                                streamId, curVersion, -1, -1);
                        }

                        first = false;
                    }
                    return first /* no data in transaction */
                        ? new CommitCheckResult(CommitDecision.WrongExpectedVersion, streamId, curVersion, -1, -1)
                        : new CommitCheckResult(CommitDecision.Idempotent, streamId, curVersion, expectedVersion + 1, eventNumber);
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
                _readers.Return(reader);
            }
        }

        void IReadIndex.UpdateTransactionInfo(long transactionId, TransactionInfo transactionInfo)
        {
            _transactionInfoCache.Put(transactionId, transactionInfo);
        }

        TransactionInfo IReadIndex.GetTransactionInfo(long writerCheckpoint, long transactionId)
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
            var seqReader = _readers.Get();
            try
            {
                seqReader.Reposition(writerCheckpoint);
                SeqReadResult result;
                while ((result = seqReader.TryReadPrev()).Success)
                {
                    if (result.LogRecord.Position < transactionId)
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
            finally
            {
                _readers.Return(seqReader);
            }
            transactionInfo = new TransactionInfo(int.MinValue, null);
            return false;
        }

        ReadIndexStats IReadIndex.GetStatistics()
        {
            return new ReadIndexStats(Interlocked.Read(ref TFChunkReader.CachedReads),
                                      Interlocked.Read(ref TFChunkReader.NotCachedReads),
                                      Interlocked.Read(ref _cachedStreamInfo),
                                      Interlocked.Read(ref _notCachedStreamInfo),
                                      Interlocked.Read(ref _cachedTransInfo),
                                      Interlocked.Read(ref _notCachedTransInfo));
        }

        private StreamMetadata GetStreamMetadataCached(ITransactionFileReader reader, string streamId)
        {
            if (SystemStreams.IsMetastream(streamId))
                return _metastreamMetadata;

            StreamCacheInfo streamCacheInfo;
            if (_streamInfoCache.TryGet(streamId, out streamCacheInfo) && streamCacheInfo.Metadata != null)
            {
                Interlocked.Increment(ref _cachedStreamInfo);
                return streamCacheInfo.Metadata;
            }

            var metadata = GetStreamMetadataUncached(reader, streamId);
            _streamInfoCache.Put(streamId,
                                 key => new StreamCacheInfo(null, metadata),
                                 // we keep previous metadata, if present by this time, because it was added on commit and is more up to date
                                 (key, oldValue) => new StreamCacheInfo(oldValue.LastEventNumber, oldValue.Metadata ?? metadata));
            return metadata;
        }

        private StreamMetadata GetStreamMetadataUncached(ITransactionFileReader reader, string streamId)
        {
            Interlocked.Increment(ref _notCachedStreamInfo);

            var metastreamId = SystemStreams.MetastreamOf(streamId);
            var metaEventNumber = GetLastStreamEventNumberCached(reader, metastreamId);
            if (metaEventNumber == ExpectedVersion.NoStream || metaEventNumber == EventNumber.DeletedStream)
                return StreamMetadata.Empty;

            EventRecord record;
            if (!GetStreamRecord(reader, metastreamId, metaEventNumber, out record))
                throw new Exception(string.Format("GetStreamRecord couldn't find metaevent #{0} on metastream '{1}'. That should never happen.", metaEventNumber, metastreamId));

            if (record.Data.Length == 0 || (record.Flags & PrepareFlags.IsJson) == 0)
                return StreamMetadata.Empty;

            try
            {
                return StreamMetadata.FromJsonBytes(record.Data);
            }
            catch (Exception)
            {
                return StreamMetadata.Empty;
            }
        }

        public void Close()
        {
            try
            {
                _tableIndex.Close(removeFiles: false);
            }
            catch (TimeoutException exc)
            {
                Log.ErrorException(exc, "Timeout exception when trying to close TableIndex.");
            }
        }

        public void Dispose()
        {
            Close();
        }

        private struct PrepareResult
        {
            public readonly bool Success;
            public readonly PrepareLogRecord Record;

            public PrepareResult(bool success, PrepareLogRecord record)
            {
                Success = success;
                Record = record;
            }
        }

        private struct EventResult
        {
            public readonly bool Success;
            public readonly EventRecord Record;

            public EventResult(bool success, EventRecord record)
            {
                Success = success;
                Record = record;
            }
        }
    }
}
