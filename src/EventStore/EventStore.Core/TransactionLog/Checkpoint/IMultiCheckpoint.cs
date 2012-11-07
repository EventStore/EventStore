using System;
using System.Net;

namespace EventStore.Core.TransactionLog.Checkpoint
{
    public interface IMultiCheckpoint: IDisposable
    {
        string Name { get; }

        void Update(IPEndPoint endPoint, long checkpoint);
        void Clear();

        void Flush();
        void Close();

        bool TryGetMinMax(out long checkpoint);
    }
}