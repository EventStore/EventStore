using System;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedSubscriber : IHandle<ClientMessage.SubscriptionConfirmation>, IHandle<ClientMessage.StreamEventAppeared>, IHandle<ClientMessage.SubscriptionDropped>
    {
        private readonly EmbeddedSubcriptionsManager _subscriptions;
        private readonly ILogger _log;
        private readonly Guid _connectionId;


        public EmbeddedSubscriber(ILogger log, Guid connectionId)
        {
            _log = log;
            _connectionId = connectionId;
            _subscriptions = new EmbeddedSubcriptionsManager();
        }

        public void Handle(ClientMessage.StreamEventAppeared message)
        {
            StreamEventAppeared(message);
        }

        public void Handle(ClientMessage.SubscriptionConfirmation message)
        {
            ConfirmSubscription(message);
        }

        public void Handle(ClientMessage.SubscriptionDropped message)
        {
            EmbeddedSubscription subscription;
            _subscriptions.TryGetActiveSubscription(message.CorrelationId, out subscription);
            subscription.DropSubscription(message.Reason);
        }

        private void StreamEventAppeared(ClientMessage.StreamEventAppeared message)
        {
            EmbeddedSubscription subscription;
            _subscriptions.TryGetActiveSubscription(message.CorrelationId, out subscription);
            subscription.EventAppeared(message.Event);
        }

        private void ConfirmSubscription(ClientMessage.SubscriptionConfirmation message)
        {
            EmbeddedSubscription subscription;
            _subscriptions.TryGetActiveSubscription(message.CorrelationId, out subscription);
            subscription.ConfirmSubscription(message.LastCommitPosition, message.LastEventNumber);
        }

        public void Start(IPublisher publisher, Guid correlationId, TaskCompletionSource<EventStoreSubscription> source, string stream, bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped)
        {
            var subscription = new EmbeddedSubscription(_log, publisher, _connectionId, source, stream, resolveLinkTos, eventAppeared,
                subscriptionDropped);

            _subscriptions.StartSubscription(correlationId, subscription);
        }
    }
}
