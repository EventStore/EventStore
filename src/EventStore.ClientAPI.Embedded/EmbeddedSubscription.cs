using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.ClientAPI.Embedded {
	internal class EmbeddedSubscription : EmbeddedSubscriptionBase<EventStoreSubscription> {
		private readonly UserCredentials _userCredentials;
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly bool _resolveLinkTos;
		private readonly Func<EventStoreSubscription, ResolvedEvent, Task> _eventAppeared;


		public EmbeddedSubscription(
			ILogger log, IPublisher publisher, Guid connectionId, TaskCompletionSource<EventStoreSubscription> source,
			string streamId, UserCredentials userCredentials, IAuthenticationProvider authenticationProvider,
			bool resolveLinkTos, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped)
			: base(log, publisher, connectionId, source, streamId, subscriptionDropped) {
			_userCredentials = userCredentials;
			_authenticationProvider = authenticationProvider;
			_resolveLinkTos = resolveLinkTos;
			_eventAppeared = eventAppeared;
		}

		override protected EventStoreSubscription CreateVolatileSubscription(long lastCommitPosition,
			long? lastEventNumber) {
			return new EmbeddedVolatileEventStoreSubscription(Unsubscribe, StreamId, lastCommitPosition,
				lastEventNumber);
		}

		public override void Start(Guid correlationId) {
			CorrelationId = correlationId;

			Publisher.PublishWithAuthentication(_authenticationProvider, _userCredentials,
				ex => DropSubscription(EventStore.Core.Services.SubscriptionDropReason.AccessDenied, ex),
				user => new ClientMessage.SubscribeToStream(
					correlationId,
					correlationId,
					new PublishEnvelope(Publisher, true),
					ConnectionId,
					StreamId,
					_resolveLinkTos,
					user));
		}


		public Task EventAppeared(Core.Data.ResolvedEvent resolvedEvent) =>
			_eventAppeared(Subscription, resolvedEvent.OriginalPosition == null
				? new ResolvedEvent(resolvedEvent.ConvertToClientResolvedIndexEvent())
				: new ResolvedEvent(resolvedEvent.ConvertToClientResolvedEvent()));
	}
}
