using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers
{
    public class ScavengeReadIndex : IReadIndex
    {
        public long LastCommitPosition { get { throw new NotImplementedException(); } }

        private readonly Dictionary<string, StreamInfo> _streams;

        public ScavengeReadIndex(Dictionary<string, StreamInfo> streams)
        {
            _streams = streams;
        }

        public void Build()
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

        public ReadEventResult ReadEvent(string streamId, int eventNumber)
        {
            throw new System.NotImplementedException();
        }

        public ReadStreamResult ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount)
        {
            throw new System.NotImplementedException();
        }

        public ReadStreamResult ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount)
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
            StreamInfo streamInfo;
            if (_streams.TryGetValue(streamId, out streamInfo))
                return streamInfo.StreamMetadata;
            return new StreamMetadata(null, null);
        }

        public CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition)
        {
            throw new System.NotImplementedException();
        }

        public void UpdateTransactionOffset(long transactionId, int transactionOffset)
        {
            throw new NotImplementedException();
        }

        public int GetTransactionOffset(long writerCheckpoint, long transactionId)
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