using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    public interface IPersistentSubscriptionCheckpointWriter
    {
        void BeginWriteState(int state);
        void BeginDeleteCheckPoint(Action<IPersistentSubscriptionCheckpointWriter> completed);
    }
}