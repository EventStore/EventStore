using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    class EventStorePersistentSubscriptionThing : EventStorePersistentSubscription
    {
        private readonly EventStoreConnectionLogicHandler _handler;

        public EventStorePersistentSubscriptionThing(
            string subscriptionId, string streamId,
            Action<EventStorePersistentSubscription, ResolvedEvent> eventAppeared,
            Action<EventStorePersistentSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
            UserCredentials userCredentials, ILogger log, bool verboseLogging, ConnectionSettings settings,
            EventStoreConnectionLogicHandler handler, int bufferSize = 10, bool autoAck = true)
            : base(
                subscriptionId, streamId, eventAppeared, subscriptionDropped, userCredentials, log, verboseLogging,
                settings, bufferSize, autoAck)
        {
            _handler = handler;
        }

        internal override PersistentEventStoreSubscription StartSubscription(
            string subscriptionId, string streamId, int bufferSize, UserCredentials userCredentials, Action<EventStoreSubscription, ResolvedEvent> onEventAppeared,
            Action<EventStoreSubscription, SubscriptionDropReason, Exception> onSubscriptionDropped, ConnectionSettings settings)
        {
            var source = new TaskCompletionSource<PersistentEventStoreSubscription>();
            _handler.EnqueueMessage(new StartPersistentSubscriptionMessage(source, subscriptionId, streamId, bufferSize,
                userCredentials, onEventAppeared,
                onSubscriptionDropped, settings.MaxRetries, settings.OperationTimeout));
            source.Task.Wait();
            return source.Task.Result;
        }
    }
}