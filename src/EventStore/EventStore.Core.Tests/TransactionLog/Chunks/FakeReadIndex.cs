using System;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    internal class FakeReadIndex: IReadIndex
    {
        public long LastCommitPosition { get { throw new NotImplementedException(); } }

        private readonly Func<string, bool> _isStreamDeleted;

        public FakeReadIndex(Func<string, bool> isStreamDeleted)
        {
            Ensure.NotNull(isStreamDeleted, "isStreamDeleted");
            _isStreamDeleted = isStreamDeleted;
        }

        public void Commit(CommitLogRecord record)
        {
            throw new NotImplementedException();
        }

        public PrepareLogRecord ReadPrepare(long pos)
        {
            throw new NotImplementedException();
        }

        public SingleReadResult ReadEvent(string streamId, int eventNumber, out EventRecord record)
        {
            throw new NotImplementedException();
        }

        public RangeReadResult ReadStreamEventsBackward(string streamId, int fromEventNumber, int maxCount, out EventRecord[] records)
        {
            throw new NotImplementedException();
        }

        public RangeReadResult ReadStreamEventsForward(string streamId, int fromEventNumber, int maxCount, out EventRecord[] records)
        {
            throw new NotImplementedException();
        }

        public int GetLastStreamEventNumber(string streamId)
        {
            throw new NotImplementedException();
        }

        public bool IsStreamDeleted(string streamId)
        {
            return _isStreamDeleted(streamId);
        }

        public ReadAllResult ReadAllEventsBackward(TFPos pos, int maxCount, bool resolveLinks)
        {
            throw new NotImplementedException();
        }

        public EventRecord ResolveLinkToEvent(EventRecord eventRecord)
        {
            throw new NotImplementedException();
        }

        public CommitCheckResult CheckCommitStartingAt(long prepareStartPosition)
        {
            throw new NotImplementedException();
        }

        public string[] GetStreamIds()
        {
            throw new NotImplementedException();
        }

        public ReadAllResult ReadAllEventsForward(TFPos pos, int maxCount, bool resolveLinks)
        {
            throw new NotImplementedException();
        }

        public int GetLastTransactionOffset(long writerCheckpoint, long transactionId)
        {
            throw new NotImplementedException();
        }

        public void Build()
        {
            throw new NotImplementedException();
        }

        public ReadIndexStats GetStatistics()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
