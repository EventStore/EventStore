using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog
{
    public interface ITransactionFileSequentialReader
    {
        RecordReadResult TryReadNext();
        RecordReadResult TryReadPrev();

        bool TryReadNext(out LogRecord record);
        bool TryReadPrev(out LogRecord record);
    }
}