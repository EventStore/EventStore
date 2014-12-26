using System;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedSubscriber : 
        IHandle<ClientMessage.SubscriptionConfirmation>, 
        IHandle<ClientMessage.StreamEventAppeared>, 
        IHandle<ClientMessage.SubscriptionDropped>,
        IHandle<ClientMessage.PersistentSubscriptionConfirmation>,
        IHandle<ClientMessage.PersistentSubscriptionStreamEventAppeared>
    {
        private readonly EmbeddedSubcriptionsManager _subscriptions;
        private readonly IPublisher _publisher;
        private readonly ILogger _log;
        private readonly Guid _connectionId;


        public EmbeddedSubscriber(IPublisher publisher, ILogger log, Guid connectionId)
        {
            _publisher = publisher;
            _log = log;
            _connectionId = connectionId;
            _subscriptions = new EmbeddedSubcriptionsManager();
        }

        public void Handle(ClientMessage.StreamEventAppeared message)
        {
            StreamEventAppeared(message.CorrelationId, message.Event);
        }

        public void Handle(ClientMessage.SubscriptionConfirmation message)
        {
            ConfirmSubscription(message.CorrelationId, message.LastCommitPosition, message.LastEventNumber);
        }

        public void Handle(ClientMessage.SubscriptionDropped message)
        {
            IEmbeddedSubscription subscription;
            _subscriptions.TryGetActiveSubscription(message.CorrelationId, out subscription);
            subscription.DropSubscription(message.Reason);
        }

        public void Handle(ClientMessage.PersistentSubscriptionConfirmation message)
        {
            ConfirmSubscription(message.CorrelationId, message.LastCommitPosition, message.LastEventNumber);
        }

        public void Handle(ClientMessage.PersistentSubscriptionStreamEventAppeared message)
        {
            StreamEventAppeared(message.CorrelationId, message.Event);
        }

        private void StreamEventAppeared(Guid correlationId, EventStore.Core.Data.ResolvedEvent resolvedEvent)
        {
            IEmbeddedSubscription subscription;
            _subscriptions.TryGetActiveSubscription(correlationId, out subscription);
            subscription.EventAppeared(resolvedEvent);
        }

        private void ConfirmSubscription(Guid correlationId, long lastCommitPosition, int? lastEventNumber)
        {
            IEmbeddedSubscription subscription;
            _subscriptions.TryGetActiveSubscription(correlationId, out subscription);
            subscription.ConfirmSubscription(lastCommitPosition, lastEventNumber);
        }

        public void Start(Guid correlationId, TaskCompletionSource<EventStoreSubscription> source, string stream, bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped)
        {
            var subscription = new EmbeddedSubscription(_log, _publisher, _connectionId, source, stream, resolveLinkTos, eventAppeared,
                subscriptionDropped);

            _subscriptions.StartSubscription(correlationId, subscription);
        }
    }
}
