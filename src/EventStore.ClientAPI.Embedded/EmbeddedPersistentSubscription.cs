using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedPersistentSubscription : EmbeddedSubscriptionBase<PersistentEventStoreSubscription>, 
        IConnectToPersistentSubscriptions
    {
        private readonly string _subscriptionId;
        private readonly UserCredentials _userCredentials;
        private readonly IAuthenticationProvider _authenticationProvider;
        private readonly int _bufferSize;

        public EmbeddedPersistentSubscription(
            ILogger log, IPublisher publisher, Guid connectionId,
            TaskCompletionSource<PersistentEventStoreSubscription> source, string subscriptionId, string streamId,
            UserCredentials userCredentials, IAuthenticationProvider authenticationProvider, int bufferSize,
            Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
            Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped, int maxRetries,
            TimeSpan operationTimeout)
            : base(log, publisher, connectionId, source, streamId, eventAppeared, subscriptionDropped)
        {
            _subscriptionId = subscriptionId;
            _userCredentials = userCredentials;
            _authenticationProvider = authenticationProvider;
            _bufferSize = bufferSize;
        }

        protected override PersistentEventStoreSubscription CreateVolatileSubscription(long lastCommitPosition, long? lastEventNumber)
        {
            return new PersistentEventStoreSubscription(this, StreamId, lastCommitPosition, lastEventNumber);
        }

        public override void Start(Guid correlationId)
        {
            CorrelationId = correlationId;

            Publisher.PublishWithAuthentication(_authenticationProvider, _userCredentials, 
                ex => DropSubscription(EventStore.Core.Services.SubscriptionDropReason.AccessDenied, ex),
                user => new ClientMessage.ConnectToPersistentSubscription(correlationId, correlationId,
                    new PublishEnvelope(Publisher, true), ConnectionId, _subscriptionId, StreamId, _bufferSize,
                    String.Empty,
                    user));
        }

        public void NotifyEventsProcessed(Guid[] processedEvents)
        {
            Ensure.NotNull(processedEvents, "processedEvents");

            Publisher.Publish(new ClientMessage.PersistentSubscriptionAckEvents(CorrelationId, CorrelationId,
                new PublishEnvelope(Publisher, true), _subscriptionId, processedEvents, SystemAccount.Principal));
        }

        public void NotifyEventsFailed(
            Guid[] processedEvents, PersistentSubscriptionNakEventAction action, string reason)
        {
            Ensure.NotNull(processedEvents, "processedEvents");
            Ensure.NotNull(reason, "reason");

            Publisher.PublishWithAuthentication(_authenticationProvider, _userCredentials,
                ex => DropSubscription(EventStore.Core.Services.SubscriptionDropReason.AccessDenied, ex),
                user => new ClientMessage.PersistentSubscriptionNackEvents(CorrelationId, CorrelationId,
                    new PublishEnvelope(Publisher, true), _subscriptionId, reason,
                    (ClientMessage.PersistentSubscriptionNackEvents.NakAction) action, processedEvents,
                    user));
        }
    }
}
