using System;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription
{
    public interface IPersistentSubscriptionEventLoader_old
    {
        void BeginLoadState(PersistentSubscription_old subscription, int startEventNumber, int countToLoad, Action<ResolvedEvent[], int> onFetchCompleted);
    }
}