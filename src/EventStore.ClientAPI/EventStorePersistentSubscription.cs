using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Represents a persistent subscription connection.
    /// </summary>
    public class EventStorePersistentSubscription : EventStorePersistentSubscriptionBase
    {
        private readonly EventStoreConnectionLogicHandler _handler;

        internal EventStorePersistentSubscription(
            string subscriptionId, string streamId,
            Func<EventStorePersistentSubscriptionBase, ResolvedEvent, Task> eventAppeared,
            Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped,
            UserCredentials userCredentials, ILogger log, bool verboseLogging, ConnectionSettings settings,
            EventStoreConnectionLogicHandler handler, int bufferSize = 10, bool autoAck = true)
            : base(
                subscriptionId, streamId, eventAppeared, subscriptionDropped, userCredentials, log, verboseLogging,
                settings, bufferSize, autoAck)
        {
            _handler = handler;
        }

        internal override Task<PersistentEventStoreSubscription> StartSubscription(
            string subscriptionId, string streamId, int bufferSize, UserCredentials userCredentials,
            Func<EventStoreSubscription, ResolvedEvent, Task> onEventAppeared,
            Action<EventStoreSubscription, SubscriptionDropReason, Exception> onSubscriptionDropped, ConnectionSettings settings)
        {
            var source = new TaskCompletionSource<PersistentEventStoreSubscription>(TaskCreationOptions.RunContinuationsAsynchronously);
            _handler.EnqueueMessage(new StartPersistentSubscriptionMessage(source, subscriptionId, streamId, bufferSize,
                userCredentials, onEventAppeared,
                onSubscriptionDropped, settings.MaxRetries, settings.OperationTimeout));

            return source.Task;
        }
    }
}
