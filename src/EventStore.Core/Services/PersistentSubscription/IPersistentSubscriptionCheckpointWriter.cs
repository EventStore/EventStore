using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    public interface IPersistentSubscriptionCheckpointWriter
    {
        void BeginWriteState(int state);
        void BeginDelete(Action<IPersistentSubscriptionCheckpointWriter> completed);
    }
}