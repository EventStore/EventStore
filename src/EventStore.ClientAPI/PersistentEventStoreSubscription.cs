using System;
using EventStore.ClientAPI.ClientOperations;

namespace EventStore.ClientAPI
{
    internal class PersistentEventStoreSubscription : EventStoreSubscription
    {
        private readonly ConnectToPersistentSubscriptionOperation _subscriptionOperation;

        internal PersistentEventStoreSubscription(ConnectToPersistentSubscriptionOperation subscriptionOperation, string streamId, long lastCommitPosition, int? lastEventNumber)
            : base(streamId, lastCommitPosition, lastEventNumber)
        {
            _subscriptionOperation = subscriptionOperation;
        }

        public override void Unsubscribe()
        {
            _subscriptionOperation.Unsubscribe();
        }

        public void NotifyEventsProcessed(Guid[] processedEvents)
        {
            _subscriptionOperation.NotifyEventsProcessed(processedEvents);
        }
    }
}