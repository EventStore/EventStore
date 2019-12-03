using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Util;

namespace EventStore.ClientAPI.Embedded {
	internal class FilteredEmbeddedSubscription : EmbeddedSubscription {
		private readonly UserCredentials _userCredentials;
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly bool _resolveLinkTos;
		private readonly TcpClientMessageDto.Filter _filter;
		private readonly Func<EventStoreSubscription, ResolvedEvent, Task> _eventAppeared;
		private readonly Func<EventStoreSubscription, Position, Task> _checkpointReached;
		private readonly int _checkpointInterval;

		public FilteredEmbeddedSubscription(
			ILogger log, IPublisher publisher, Guid connectionId, TaskCompletionSource<EventStoreSubscription> source,
			string streamId, UserCredentials userCredentials, IAuthenticationProvider authenticationProvider,
			bool resolveLinkTos, TcpClientMessageDto.Filter filter,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Func<EventStoreSubscription, Position, Task> checkpointReached, int checkpointInterval,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped)
			: base(log, publisher, connectionId, source, streamId, userCredentials, authenticationProvider,
				resolveLinkTos, eventAppeared, subscriptionDropped) {
			_userCredentials = userCredentials;
			_authenticationProvider = authenticationProvider;
			_resolveLinkTos = resolveLinkTos;
			_filter = filter;
			_eventAppeared = eventAppeared;
			_checkpointReached = checkpointReached;
			_checkpointInterval = checkpointInterval;
		}

		override protected EventStoreSubscription CreateVolatileSubscription(long lastCommitPosition,
			long? lastEventNumber) {
			return new EmbeddedVolatileEventStoreSubscription(Unsubscribe, StreamId, lastCommitPosition,
				lastEventNumber);
		}

		public override void Start(Guid correlationId) {
			CorrelationId = correlationId;

			Publisher.PublishWithAuthentication(_authenticationProvider, _userCredentials,
				ex => DropSubscription(Core.Services.SubscriptionDropReason.AccessDenied, ex),
				user => new ClientMessage.FilteredSubscribeToStream(
					correlationId,
					correlationId,
					new PublishEnvelope(Publisher, true),
					ConnectionId,
					StreamId,
					_resolveLinkTos,
					user,
					EventFilter.Get(_filter),
					_checkpointInterval));
		}

		public void CheckpointReached(TFPos? messagePosition) {
			_checkpointReached(Subscription,
				new Position(messagePosition.Value.CommitPosition, messagePosition.Value.PreparePosition));
		}
	}
}
