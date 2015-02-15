using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedSubscription : EmbeddedSubscriptionBase<EventStoreSubscription>
    {
        private readonly UserCredentials _userCredentials;
        private readonly IAuthenticationProvider _authenticationProvider;
        private readonly bool _resolveLinkTos;


        public EmbeddedSubscription(
            ILogger log, IPublisher publisher, Guid connectionId, TaskCompletionSource<EventStoreSubscription> source,
            string streamId, UserCredentials userCredentials, IAuthenticationProvider authenticationProvider,
            bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
            Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped)
            : base(log, publisher, connectionId, source, streamId, eventAppeared, subscriptionDropped)
        {
            _userCredentials = userCredentials;
            _authenticationProvider = authenticationProvider;
            _resolveLinkTos = resolveLinkTos;
        }

        override protected EventStoreSubscription CreateVolatileSubscription(long lastCommitPosition, int? lastEventNumber)
        {
            return new EmbeddedVolatileEventStoreSubscription(Unsubscribe, StreamId, lastCommitPosition, lastEventNumber);
        }

        public override void Start(Guid correlationId)
        {
            CorrelationId = correlationId;

            Publisher.PublishWithAuthentication(_authenticationProvider, _userCredentials, 
                _ => DropSubscription(EventStore.Core.Services.SubscriptionDropReason.AccessDenied), 
                user => new ClientMessage.SubscribeToStream(
                    correlationId,
                    correlationId,
                    new PublishEnvelope(Publisher, true),
                    ConnectionId,
                    StreamId,
                    _resolveLinkTos,
                    user));
        }
    }
}