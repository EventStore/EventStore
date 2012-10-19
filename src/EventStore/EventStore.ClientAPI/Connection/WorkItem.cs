using System;
using System.Threading;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI.Connection
{
    internal class WorkItem
    {
        private static long _seqNumber = -1;

        public readonly long SeqNo;
        public IClientOperation Operation;

        public int Attempt;
        public long LastUpdatedTicks;

        public WorkItem(IClientOperation operation)
        {
            Ensure.NotNull(operation, "operation");
            SeqNo = NextSeqNo();
            Operation = operation;

            Attempt = 0;
            LastUpdatedTicks = DateTime.UtcNow.Ticks;
        }

        private static long NextSeqNo()
        {
            return Interlocked.Increment(ref _seqNumber);
        }

        public override string ToString()
        {
            return string.Format("WorkItem {0}: {1}, attempt: {2}, seqNo: {3}", 
                                 Operation.GetType().FullName, 
                                 Operation,
                                 Attempt, 
                                 SeqNo);
        }
    }
}