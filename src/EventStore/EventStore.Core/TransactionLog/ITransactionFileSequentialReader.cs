using System;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog
{
    public interface ITransactionFileSequentialReader: IDisposable
    {
        void Open();
        void Close();

        void Reposition(long position);

        SeqReadResult TryReadNext();
        SeqReadResult TryReadNextNonFlushed();
        SeqReadResult TryReadPrev();
        SeqReadResult TryReadPrevNonFlushed();

        bool TryReadNext(out LogRecord record);
        bool TryReadPrev(out LogRecord record);
    }
}