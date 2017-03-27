using System;

namespace EventStore.ClientAPI.Embedded
{
    internal interface IEmbeddedSubscription
    {
        void DropSubscription(EventStore.Core.Services.SubscriptionDropReason reason, Exception ex = null);
        void EventAppeared(EventStore.Core.Data.ResolvedEvent resolvedEvent);
        void ConfirmSubscription(long lastCommitPosition, long? lastEventNumber);
        void Unsubscribe();
        void Start(Guid correlationId);
    }
}