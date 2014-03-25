using System;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public interface IIndexReader
    {
        long CachedStreamInfo { get; }
        long NotCachedStreamInfo { get; }
        long HashCollisions { get; }

        IndexReadEventResult ReadEvent(string streamId, int eventNumber);
        IndexReadStreamResult ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount);
        IndexReadStreamResult ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount);

        /// <summary>
        /// Doesn't filter $maxAge, $maxCount, $tb(truncate before), doesn't check stream deletion, etc.
        /// </summary>
        PrepareLogRecord ReadPrepare(string streamId, int eventNumber);

        string GetEventStreamIdByTransactionId(long transactionId); 
        StreamAccess CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user);
        
        StreamMetadata GetStreamMetadata(string streamId);
        int GetStreamLastEventNumber(string streamId);
    }

    public class IndexReader : IIndexReader
    {
        public long CachedStreamInfo { get { return Interlocked.Read(ref _cachedStreamInfo); } }
        public long NotCachedStreamInfo { get { return Interlocked.Read(ref _notCachedStreamInfo); } }
        public long HashCollisions { get { return Interlocked.Read(ref _hashCollisions); } }

        private readonly IIndexBackend _backend;
        private readonly IHasher _hasher;
        private readonly ITableIndex _tableIndex;
        private readonly StreamMetadata _metastreamMetadata;

        private long _hashCollisions;
        private long _cachedStreamInfo;
        private long _notCachedStreamInfo;

        public IndexReader(IIndexBackend backend, IHasher hasher, ITableIndex tableIndex, StreamMetadata metastreamMetadata)
        {
            Ensure.NotNull(backend, "backend");
            Ensure.NotNull(hasher, "hasher");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(metastreamMetadata, "metastreamMetadata");

            _backend = backend;
            _hasher = hasher;
            _tableIndex = tableIndex;
            _metastreamMetadata = metastreamMetadata;
        }


        IndexReadEventResult IIndexReader.ReadEvent(string streamId, int eventNumber)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");
            if (eventNumber < -1) throw new ArgumentOutOfRangeException("eventNumber");
            using (var reader = _backend.BorrowReader())
            {
                return ReadEventInternal(reader, streamId, eventNumber);
            }
        }

        private IndexReadEventResult ReadEventInternal(TFReaderLease reader, string streamId, int eventNumber)
        {
            var lastEventNumber = GetStreamLastEventNumberCached(reader, streamId);
            var metadata = GetStreamMetadataCached(reader, streamId);
            if (lastEventNumber == EventNumber.DeletedStream)
                return new IndexReadEventResult(ReadEventResult.StreamDeleted, metadata, lastEventNumber);
            if (lastEventNumber == ExpectedVersion.NoStream || metadata.TruncateBefore == EventNumber.DeletedStream)
                return new IndexReadEventResult(ReadEventResult.NoStream, metadata, lastEventNumber);

            if (eventNumber == -1)
                eventNumber = lastEventNumber;

            int minEventNumber = 0;
            if (metadata.MaxCount.HasValue)
                minEventNumber = Math.Max(minEventNumber, lastEventNumber - metadata.MaxCount.Value + 1);
            if (metadata.TruncateBefore.HasValue)
                minEventNumber = Math.Max(minEventNumber, metadata.TruncateBefore.Value);

            if (eventNumber < minEventNumber || eventNumber > lastEventNumber)
                return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber);

            PrepareLogRecord prepare = ReadPrepareInternal(reader, streamId, eventNumber);
            if (prepare != null)
            {
                if (metadata.MaxAge.HasValue && prepare.TimeStamp < DateTime.UtcNow - metadata.MaxAge.Value)
                    return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber);
                return new IndexReadEventResult(ReadEventResult.Success, new EventRecord(eventNumber, prepare), metadata, lastEventNumber);
            }

            return new IndexReadEventResult(ReadEventResult.NotFound, metadata, lastEventNumber);
        }

        PrepareLogRecord IIndexReader.ReadPrepare(string streamId, int eventNumber)
        {
            using (var reader = _backend.BorrowReader())
            {
                return ReadPrepareInternal(reader, streamId, eventNumber);
            }
        }

        private PrepareLogRecord ReadPrepareInternal(TFReaderLease reader, string streamId, int eventNumber)
        {
            // we assume that you already did check for stream deletion
            Ensure.NotNullOrEmpty(streamId, "streamId");
            Ensure.Nonnegative(eventNumber, "eventNumber");

            var streamHash = _hasher.Hash(streamId);
            long position;
            if (_tableIndex.TryGetOneValue(streamHash, eventNumber, out position))
            {
                var rec = ReadPrepareInternal(reader, position);
                if (rec != null && rec.EventStreamId == streamId)
                    return rec;

                foreach (var indexEntry in _tableIndex.GetRange(streamHash, eventNumber, eventNumber))
                {
                    Interlocked.Increment(ref _hashCollisions);
                    if (indexEntry.Position == position) // already checked that
                        continue;
                    rec = ReadPrepareInternal(reader, indexEntry.Position);
                    if (rec != null && rec.EventStreamId == streamId)
                        return rec;
                }
            }
            return null;
        }

        private static PrepareLogRecord ReadPrepareInternal(TFReaderLease reader, long logPosition)
        {
            RecordReadResult result = reader.TryReadAt(logPosition);
            if (!result.Success)
                return null;
            if (result.LogRecord.RecordType != LogRecordType.Prepare)
                throw new Exception(string.Format("Incorrect type of log record {0}, expected Prepare record.",
                                                  result.LogRecord.RecordType));
            return (PrepareLogRecord)result.LogRecord;
        }

        IndexReadStreamResult IIndexReader.ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");
            Ensure.Nonnegative(fromEventNumber, "fromEventNumber");
            Ensure.Positive(maxCount, "maxCount");

            var streamHash = _hasher.Hash(streamId);
            using (var reader = _backend.BorrowReader())
            {
                var lastEventNumber = GetStreamLastEventNumberCached(reader, streamId);
                var metadata = GetStreamMetadataCached(reader, streamId);
                if (lastEventNumber == EventNumber.DeletedStream)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.StreamDeleted, StreamMetadata.Empty, lastEventNumber);
                if (lastEventNumber == ExpectedVersion.NoStream || metadata.TruncateBefore == EventNumber.DeletedStream)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.NoStream, metadata, lastEventNumber);

                int startEventNumber = fromEventNumber;
                int endEventNumber = (int) Math.Min(int.MaxValue, (long) fromEventNumber + maxCount - 1);

                int minEventNumber = 0;
                if (metadata.MaxCount.HasValue)
                    minEventNumber = Math.Max(minEventNumber, lastEventNumber - metadata.MaxCount.Value + 1);
                if (metadata.TruncateBefore.HasValue) 
                    minEventNumber = Math.Max(minEventNumber, metadata.TruncateBefore.Value);
                if (endEventNumber < minEventNumber)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
                                                     metadata, minEventNumber, lastEventNumber, isEndOfStream: false);
                startEventNumber = Math.Max(startEventNumber, minEventNumber);

                var recordsQuery = _tableIndex.GetRange(streamHash, startEventNumber, endEventNumber)
                                              .Select(x => new {x.Version, Prepare = ReadPrepareInternal(reader, x.Position)})
                                              .Where(x => x.Prepare != null && x.Prepare.EventStreamId == streamId);

                if (metadata.MaxAge.HasValue)
                {
                    var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
                    recordsQuery = recordsQuery.Where(x => x.Prepare.TimeStamp >= ageThreshold);
                }

                var records = recordsQuery.Reverse().Select(x => new EventRecord(x.Version, x.Prepare)).ToArray();
                
                int nextEventNumber = Math.Min(endEventNumber + 1, lastEventNumber + 1);
                if (records.Length > 0)
                    nextEventNumber = records[records.Length - 1].EventNumber + 1;
                var isEndOfStream = endEventNumber >= lastEventNumber;
                return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
                                                 nextEventNumber, lastEventNumber, isEndOfStream);
            }
        }

        IndexReadStreamResult IIndexReader.ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");
            Ensure.Positive(maxCount, "maxCount");

            var streamHash = _hasher.Hash(streamId);
            using (var reader = _backend.BorrowReader())
            {
                var lastEventNumber = GetStreamLastEventNumberCached(reader, streamId);
                var metadata = GetStreamMetadataCached(reader, streamId);
                if (lastEventNumber == EventNumber.DeletedStream)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.StreamDeleted, StreamMetadata.Empty, lastEventNumber);
                if (lastEventNumber == ExpectedVersion.NoStream || metadata.TruncateBefore == EventNumber.DeletedStream)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, ReadStreamResult.NoStream, metadata, lastEventNumber);

                int endEventNumber = fromEventNumber < 0 ? lastEventNumber : fromEventNumber;
                int startEventNumber = (int)Math.Max(0L, (long)endEventNumber - maxCount + 1);
                bool isEndOfStream = false;

                int minEventNumber = 0;
                if (metadata.MaxCount.HasValue)
                    minEventNumber = Math.Max(minEventNumber, lastEventNumber - metadata.MaxCount.Value + 1);
                if (metadata.TruncateBefore.HasValue)
                    minEventNumber = Math.Max(minEventNumber, metadata.TruncateBefore.Value);
                if (endEventNumber < minEventNumber)
                    return new IndexReadStreamResult(fromEventNumber, maxCount, IndexReadStreamResult.EmptyRecords,
                                                     metadata, -1, lastEventNumber, isEndOfStream: true);

                if (startEventNumber <= minEventNumber)
                {
                    isEndOfStream = true;
                    startEventNumber = minEventNumber;
                }

                var recordsQuery = _tableIndex.GetRange(streamHash, startEventNumber, endEventNumber)
                                              .Select(x => new {x.Version, Prepare = ReadPrepareInternal(reader, x.Position)})
                                              .Where(x => x.Prepare != null && x.Prepare.EventStreamId == streamId);

                if (metadata.MaxAge.HasValue)
                {
                    var ageThreshold = DateTime.UtcNow - metadata.MaxAge.Value;
                    recordsQuery = recordsQuery.Where(x => x.Prepare.TimeStamp >= ageThreshold);
                }

                var records = recordsQuery.Select(x => new EventRecord(x.Version, x.Prepare)).ToArray();

                isEndOfStream = isEndOfStream 
                                || startEventNumber == 0 
                                || (startEventNumber <= lastEventNumber 
                                   && (records.Length == 0 || records[records.Length - 1].EventNumber != startEventNumber));
                int nextEventNumber = isEndOfStream ? -1 : Math.Min(startEventNumber - 1, lastEventNumber);
                return new IndexReadStreamResult(endEventNumber, maxCount, records, metadata,
                                                 nextEventNumber, lastEventNumber, isEndOfStream);
            }
        }

        public string GetEventStreamIdByTransactionId(long transactionId)
        {
            Ensure.Nonnegative(transactionId, "transactionId");
            using (var reader = _backend.BorrowReader())
            {
                var res = ReadPrepareInternal(reader, transactionId);
                return res == null ? null : res.EventStreamId;
            }
        }


        StreamAccess IIndexReader.CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");
            using (var reader = _backend.BorrowReader())
            {
                return CheckStreamAccessInternal(reader, streamId, streamAccessType, user);
            }
        }

        private StreamAccess CheckStreamAccessInternal(TFReaderLease reader, string streamId,
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
                        return new StreamAccess(false);
                    default:
                        throw new ArgumentOutOfRangeException("streamAccessType");
                }
            }

            if ((streamAccessType == StreamAccessType.Write || streamAccessType == StreamAccessType.Delete)
                && streamId == SystemStreams.AllStream)
                return new StreamAccess(false);

            var sysSettings = _backend.GetSystemSettings() ?? SystemSettings.Default;
            var meta = GetStreamMetadataCached(reader, streamId);
            StreamAcl acl;
            StreamAcl sysAcl;
            StreamAcl defAcl;
            if (SystemStreams.IsSystemStream(streamId))
            {
                defAcl = SystemSettings.Default.SystemStreamAcl;
                sysAcl = sysSettings.SystemStreamAcl ?? defAcl;
                acl = meta.Acl ?? sysAcl;
            }
            else
            {
                defAcl = SystemSettings.Default.UserStreamAcl;
                sysAcl = sysSettings.UserStreamAcl ?? defAcl;
                acl = meta.Acl ?? sysAcl;
            }
            string[] roles;
            switch (streamAccessType)
            {
                case StreamAccessType.Read: roles = acl.ReadRoles ?? sysAcl.ReadRoles ?? defAcl.ReadRoles; break;
                case StreamAccessType.Write: roles = acl.WriteRoles ?? sysAcl.WriteRoles ?? defAcl.WriteRoles; break;
                case StreamAccessType.Delete: roles = acl.DeleteRoles ?? sysAcl.DeleteRoles ?? defAcl.DeleteRoles; break;
                case StreamAccessType.MetaRead: roles = acl.MetaReadRoles ?? sysAcl.MetaReadRoles ?? defAcl.MetaReadRoles; break;
                case StreamAccessType.MetaWrite: roles = acl.MetaWriteRoles ?? sysAcl.MetaWriteRoles ?? defAcl.MetaWriteRoles; break;
                default: throw new ArgumentOutOfRangeException("streamAccessType");
            }

            var isPublic = roles.Contains(x => x == SystemRoles.All);
            if (isPublic) return new StreamAccess(true, true);
            if (user == null) return new StreamAccess(false);
            if (user.IsInRole(SystemRoles.Admins)) return new StreamAccess(true);
            for (int i = 0; i < roles.Length; ++i)
            {
                if (user.IsInRole(roles[i]))
                    return new StreamAccess(true);
            }
            return new StreamAccess(false);
        }

        int IIndexReader.GetStreamLastEventNumber(string streamId)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");
            using (var reader = _backend.BorrowReader())
            {
                return GetStreamLastEventNumberCached(reader, streamId);
            }
        }

        StreamMetadata IIndexReader.GetStreamMetadata(string streamId)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");
            using (var reader = _backend.BorrowReader())
            {
                return GetStreamMetadataCached(reader, streamId);
            }
        }

        private int GetStreamLastEventNumberCached(TFReaderLease reader, string streamId)
        {
            // if this is metastream -- check if original stream was deleted, if yes -- metastream is deleted as well
            if (SystemStreams.IsMetastream(streamId)
                && GetStreamLastEventNumberCached(reader, SystemStreams.OriginalStreamOf(streamId)) == EventNumber.DeletedStream)
                return EventNumber.DeletedStream;

            var cache = _backend.TryGetStreamLastEventNumber(streamId);
            if (cache.LastEventNumber != null)
            {
                Interlocked.Increment(ref _cachedStreamInfo);
                return cache.LastEventNumber.GetValueOrDefault();
            }

            Interlocked.Increment(ref _notCachedStreamInfo);
            var lastEventNumber = GetStreamLastEventNumberUncached(reader, streamId);

            // Conditional update depending on previously returned cache info version.
            // If version is not correct -- nothing is changed in cache.
            // This update is conditioned to not interfere with updating stream cache info by commit procedure
            // (which is the source of truth).
            var res = _backend.UpdateStreamLastEventNumber(cache.Version, streamId, lastEventNumber);
            return res ?? lastEventNumber;
        }

        private int GetStreamLastEventNumberUncached(TFReaderLease reader, string streamId)
        {
            var streamHash = _hasher.Hash(streamId);
            IndexEntry latestEntry;
            if (!_tableIndex.TryGetLatestEntry(streamHash, out latestEntry))
                return ExpectedVersion.NoStream;

            var rec = ReadPrepareInternal(reader, latestEntry.Position);
            if (rec == null) throw new Exception("Couldn't read latest stream's prepare! That shouldn't happen EVER!");
            if (rec.EventStreamId == streamId) // LUCKY!!!
                return latestEntry.Version;

            // TODO AN here lies the problem of out of memory if the stream has A LOT of events in them
            foreach (var indexEntry in _tableIndex.GetRange(streamHash, 0, int.MaxValue))
            {
                var r = ReadPrepareInternal(reader, indexEntry.Position);
                if (r != null && r.EventStreamId == streamId)
                    return indexEntry.Version; // AT LAST!!!
                Interlocked.Increment(ref _hashCollisions);
            }
            return ExpectedVersion.NoStream; // no such event stream
        }

        private StreamMetadata GetStreamMetadataCached(TFReaderLease reader, string streamId)
        {
            // if this is metastream -- check if original stream was deleted, if yes -- metastream is deleted as well
            if (SystemStreams.IsMetastream(streamId))
                return _metastreamMetadata;

            var cache = _backend.TryGetStreamMetadata(streamId);
            if (cache.Metadata != null)
            {
                Interlocked.Increment(ref _cachedStreamInfo);
                return cache.Metadata;
            }

            Interlocked.Increment(ref _notCachedStreamInfo);
            var streamMetadata = GetStreamMetadataUncached(reader, streamId);

            // Conditional update depending on previously returned cache info version.
            // If version is not correct -- nothing is changed in cache.
            // This update is conditioned to not interfere with updating stream cache info by commit procedure
            // (which is the source of truth).
            var res = _backend.UpdateStreamMetadata(cache.Version, streamId, streamMetadata);
            return res ?? streamMetadata;
        }

        private StreamMetadata GetStreamMetadataUncached(TFReaderLease reader, string streamId)
        {
            var metastreamId = SystemStreams.MetastreamOf(streamId);
            var metaEventNumber = GetStreamLastEventNumberCached(reader, metastreamId);
            if (metaEventNumber == ExpectedVersion.NoStream || metaEventNumber == EventNumber.DeletedStream)
                return StreamMetadata.Empty;

            PrepareLogRecord prepare = ReadPrepareInternal(reader, metastreamId, metaEventNumber);
            if (prepare == null)
                throw new Exception(string.Format("ReadPrepareInternal couldn't find metaevent #{0} on metastream '{1}'. "
                                                  + "That should never happen.", metaEventNumber, metastreamId));

            if (prepare.Data.Length == 0 || prepare.Flags.HasNoneOf(PrepareFlags.IsJson))
                return StreamMetadata.Empty;

            try
            {
                return StreamMetadata.FromJsonBytes(prepare.Data);
            }
            catch (Exception)
            {
                return StreamMetadata.Empty;
            }
        }
    }
}