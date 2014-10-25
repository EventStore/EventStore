using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    [Flags]
    public enum PersistentSubscriptionState
    {
        NotReady = 0x00,
        OutstandingPageRequest = 0x01,
        Replaying = 0x02,
        Live = 0x04,
    }
}