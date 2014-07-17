using System;
using EventStore.ClientAPI.ClientOperations;

namespace EventStore.ClientAPI
{
    internal class PersistentEventStoreSubscription : EventStoreSubscription
    {
        private readonly PersistentSubscriptionOperation _subscriptionOperation;

        internal PersistentEventStoreSubscription(PersistentSubscriptionOperation subscriptionOperation, string streamId, long lastCommitPosition, int? lastEventNumber)
            : base(streamId, lastCommitPosition, lastEventNumber)
        {
            _subscriptionOperation = subscriptionOperation;
        }

        public override void Unsubscribe()
        {
            _subscriptionOperation.Unsubscribe();
        }

        public void NotifyEventsProcessed(int freeSlots, Guid[] processedEvents)
        {
            _subscriptionOperation.NotifyEventsProcessed(freeSlots, processedEvents);
        }
    }
}