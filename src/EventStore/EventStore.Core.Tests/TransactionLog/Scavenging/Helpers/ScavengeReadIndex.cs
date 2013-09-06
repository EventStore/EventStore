using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers
{
    public class ScavengeReadIndex : IReadIndex
    {
        public long LastCommitPosition { get { throw new NotImplementedException(); } }
        public IIndexWriter IndexWriter { get { throw new NotImplementedException(); } }

        private readonly Dictionary<string, StreamInfo> _streams;
        private readonly int _metastreamMaxCount;

        public ScavengeReadIndex(Dictionary<string, StreamInfo> streams, int metastreamMaxCount)
        {
            _streams = streams;
            _metastreamMaxCount = metastreamMaxCount;
        }

        public void Init(long buildToPosition)
        {
        }

        public void Commit(CommitLogRecord record)
        {
            throw new NotImplementedException();
        }

        public void Commit(IList<PrepareLogRecord> commitedPrepares)
        {
            throw new NotImplementedException();
        }

        public ReadIndexStats GetStatistics()
        {
            throw new NotImplementedException();
        }

        public IndexReadEventResult ReadEvent(string streamId, int eventNumber)
        {
            throw new NotImplementedException();
        }

        public IndexReadStreamResult ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount)
        {
            throw new NotImplementedException();
        }

        public IndexReadStreamResult ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount)
        {
            throw new NotImplementedException();
        }

        public IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount)
        {
            throw new NotImplementedException();
        }

        public IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount)
        {
            throw new NotImplementedException();
        }

        public Core.Services.Storage.ReaderIndex.StreamInfo GetStreamInfo(string streamId)
        {
            return new Core.Services.Storage.ReaderIndex.StreamInfo(GetLastStreamEventNumber(streamId), GetStreamMetadata(streamId));
        }

        public bool IsStreamDeleted(string streamId)
        {
            if (SystemStreams.IsMetastream(streamId))
                streamId = SystemStreams.OriginalStreamOf(streamId);

            StreamInfo streamInfo;
            return _streams.TryGetValue(streamId, out streamInfo) && streamInfo.StreamVersion == EventNumber.DeletedStream;
        }

        public int GetLastStreamEventNumber(string streamId)
        {
            if (IsStreamDeleted(streamId))
                return EventNumber.DeletedStream;

            StreamInfo streamInfo;
            if (_streams.TryGetValue(streamId, out streamInfo))
                return streamInfo.StreamVersion;
            return -1;
        }

        public string GetEventStreamIdByTransactionId(long transactionId)
        {
            throw new NotImplementedException();
        }

        public StreamAccess CheckStreamAccess(string streamId, StreamAccessType streamAccessType, IPrincipal user)
        {
            throw new NotImplementedException();
        }

        public StreamMetadata GetStreamMetadata(string streamId)
        {
            if (SystemStreams.IsMetastream(streamId))
                return new StreamMetadata(_metastreamMaxCount, null, null, null, null);

            StreamInfo streamInfo;
            if (_streams.TryGetValue(streamId, out streamInfo))
                return streamInfo.StreamMetadata ?? StreamMetadata.Empty;
            return new StreamMetadata(null, null, null, null, null);
        }

        public void Close()
        {
        }

        public void Dispose()
        {
        }
    }
}