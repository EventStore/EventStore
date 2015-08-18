using System;

namespace EventStore.ClientAPI.Embedded
{
    internal interface IEmbeddedSubscription
    {
        void DropSubscription(EventStore.Core.Services.SubscriptionDropReason reason);
        void EventAppeared(EventStore.Core.Data.ResolvedEvent resolvedEvent);
        void ConfirmSubscription(long lastCommitPosition, int? lastEventNumber);
        void Unsubscribe();
        void Start(Guid correlationId);
    }
}