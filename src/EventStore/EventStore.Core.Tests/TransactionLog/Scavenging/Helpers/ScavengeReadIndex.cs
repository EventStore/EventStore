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

        private readonly Dictionary<string, int> _streams;

        public ScavengeReadIndex(Dictionary<string, int> streams)
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
            int streamVersion;
            return _streams.TryGetValue(streamId, out streamVersion) && streamVersion == EventNumber.DeletedStream;
        }

        public int GetLastStreamEventNumber(string streamId)
        {
            int streamVersion;
            if (_streams.TryGetValue(streamId, out streamVersion))
                return streamVersion;
            return -1;
        }

        public StreamMetadata GetStreamMetadata(string streamId)
        {
            return new StreamMetadata(null, null);
        }

        public CommitCheckResult CheckCommitStartingAt(long transactionPosition, long commitPosition)
        {
            throw new System.NotImplementedException();
        }

        public int GetLastTransactionOffset(long writerCheckpoint, long transactionId)
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