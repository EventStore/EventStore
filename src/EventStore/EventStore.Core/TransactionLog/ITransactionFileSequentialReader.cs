using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog
{
    public interface ITransactionFileSequentialReader: IDisposable
    {
        long Position { get; }

        void Open();
        void Close();

        void Reposition(long position);

        RecordReadResult TryReadNext();
        RecordReadResult TryReadPrev();

        bool TryReadNext(out LogRecord record);
        bool TryReadPrev(out LogRecord record);
    }
}