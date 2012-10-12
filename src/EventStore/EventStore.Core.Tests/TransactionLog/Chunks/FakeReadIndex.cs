using System;
using System.Collections.Generic;
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

        public PrepareLogRecord GetPrepare(long pos)
        {
            throw new NotImplementedException();
        }

        public SingleReadResult TryReadRecord(string eventStreamId, int version, out EventRecord record)
        {
            throw new NotImplementedException();
        }

        public RangeReadResult TryReadRecordsBackward(string eventStreamId, int fromEventNumber, int maxCount, out EventRecord[] records)
        {
            throw new NotImplementedException();
        }

        public RangeReadResult TryReadEventsForward(string eventStreamId, int fromEventNumber, int maxCount, out EventRecord[] records)
        {
            throw new NotImplementedException();
        }

        public int GetLastStreamEventNumber(string eventStreamId)
        {
            throw new NotImplementedException();
        }

        public bool IsStreamDeleted(string eventStreamId)
        {
            return _isStreamDeleted(eventStreamId);
        }

        public List<ResolvedEventRecord> ReadAllEventsForward(long commitPos, long preparePos, bool inclusivePos, int maxCount, bool resolveLinks)
        {
            throw new NotImplementedException();
        }

        public List<ResolvedEventRecord> ReadAllEventsBackward(long commitPos, long preparePos, bool inclusivePos, int maxCount, bool resolveLinks)
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
