using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedSubscription : EmbeddedSubscriptionBase<EventStoreSubscription>
    {
        private readonly bool _resolveLinkTos;

        public EmbeddedSubscription(ILogger log, IPublisher publisher, Guid connectionId, TaskCompletionSource<EventStoreSubscription> source, string streamId, bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped)
            : base(log, publisher, connectionId, source, streamId, eventAppeared, subscriptionDropped)
        {
            _resolveLinkTos = resolveLinkTos;
        }

        override protected EventStoreSubscription CreateVolatileSubscription(long lastCommitPosition, int? lastEventNumber)
        {
            return new EmbeddedVolatileEventStoreSubscription(Unsubscribe, StreamId, lastCommitPosition, lastEventNumber);
        }

        public override void Start(Guid correlationId)
        {
            CorrelationId = correlationId;

            Publisher.Publish(new ClientMessage.SubscribeToStream(
                correlationId, 
                correlationId, 
                new PublishEnvelope(Publisher, true), 
                ConnectionId, 
                StreamId,
                _resolveLinkTos, 
                SystemAccount.Principal));
        }
    }
}