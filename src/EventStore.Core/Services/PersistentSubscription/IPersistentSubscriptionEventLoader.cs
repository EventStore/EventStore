using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription
{
    public interface IPersistentSubscriptionEventLoader
    {
        void BeginLoadState(PersistentSubscription subscription, int startEventNumber, int countToLoad, Action<ResolvedEvent[], int> onFetchCompleted);
    }
}