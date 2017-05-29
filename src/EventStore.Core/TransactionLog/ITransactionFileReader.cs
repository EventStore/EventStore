using System;
using EventStore.Core.DataStructures;

namespace EventStore.Core.TransactionLog
{
    public interface ITransactionFileReader
    {
        void Reposition(long position);

        SeqReadResult TryReadNext();
        SeqReadResult TryReadPrev();

        RecordReadResult TryReadAt(long position);
        bool ExistsAt(long position);
    }

    public struct TFReaderLease : IDisposable
    {
        public readonly ITransactionFileReader Reader;
        private readonly ObjectPool<ITransactionFileReader> _pool;

        public TFReaderLease(ObjectPool<ITransactionFileReader> pool)
        {
            _pool = pool;
            Reader = pool.Get();
        }

        public TFReaderLease(ITransactionFileReader reader)
        {
            _pool = null;
            Reader = reader;
        }

        void IDisposable.Dispose() => _pool?.Return(Reader);

        public void Reposition(long position) => Reader.Reposition(position);

        public SeqReadResult TryReadNext() => Reader.TryReadNext();

        public SeqReadResult TryReadPrev() => Reader.TryReadPrev();

        public bool ExistsAt(long position) => Reader.ExistsAt(position);

        public RecordReadResult TryReadAt(long position) => Reader.TryReadAt(position);
    }
}