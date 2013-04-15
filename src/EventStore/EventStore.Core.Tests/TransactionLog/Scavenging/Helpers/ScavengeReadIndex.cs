using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers
{
    public class ScavengeReadIndex : IReadIndex
    {
        public long LastCommitPosition { get { throw new NotImplementedException(); } }

        private readonly Dictionary<string, StreamInfo> _streams;
        private readonly int _metastreamMaxCount;

        public ScavengeReadIndex(Dictionary<string, StreamInfo> streams, int metastreamMaxCount)
        {
            _streams = streams;
            _metastreamMaxCount = metastreamMaxCount;
        }

        public void Init(long writerCheckpoint, long buildToPosition)
        {
        }

        public void Commit(CommitLogRecord record)
        {
            throw new System.NotImplementedException();
        }

        public ReadIndexStats GetStatistics()
        {
            throw new System.NotImplementedException();
        }

        public IndexReadEventResult ReadEvent(string streamId, int eventNumber)
        {
            throw new System.NotImplementedException();
        }

        public IndexReadStreamResult ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount)
        {
            throw new System.NotImplementedException();
        }

        public IndexReadStreamResult ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount)
        {
            throw new System.NotImplementedException();
        }

        public IndexReadAllResult ReadAllEventsForward(TFPos pos, int maxCount)
        {
            throw new System.NotImplementedException();
        }

        public IndexReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount)
        {
            throw new System.NotImplementedException();
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
            StreamInfo streamInfo;
            if (_streams.TryGetValue(streamId, out streamInfo))
                return streamInfo.StreamVersion;
            return -1;
        }

        public StreamMetadata GetStreamMetadata(string streamId)
        {
            if (SystemStreams.IsMetastream(streamId))
                return new StreamMetadata(_metastreamMaxCount, null);

            StreamInfo streamInfo;
            if (_streams.TryGetValue(streamId, out streamInfo))
                return streamInfo.StreamMetadata;
            return new StreamMetadata(null, null);
        }

        public CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition)
        {
            throw new System.NotImplementedException();
        }

        public void UpdateTransactionInfo(long transactionId, Core.Services.Storage.ReaderIndex.TransactionInfo transactionInfo)
        {
            throw new NotImplementedException();
        }

        public Core.Services.Storage.ReaderIndex.TransactionInfo GetTransactionInfo(long writerCheckpoint, long transactionId)
        {
            throw new System.NotImplementedException();
        }

        public void Close()
        {
        }

        public void Dispose()
        {
        }
    }
}