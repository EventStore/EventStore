using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.ClientAPI.Embedded {
	internal class EmbeddedSubscriber :
		IHandle<ClientMessage.SubscriptionConfirmation>,
		IHandle<ClientMessage.StreamEventAppeared>,
		IHandle<ClientMessage.SubscriptionDropped>,
		IHandle<ClientMessage.CheckpointReached>,
		IHandle<ClientMessage.PersistentSubscriptionConfirmation>,
		IHandle<ClientMessage.PersistentSubscriptionStreamEventAppeared> {
		private readonly EmbeddedSubcriptionsManager _subscriptions;
		private readonly IPublisher _publisher;
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly ILogger _log;
		private readonly Guid _connectionId;


		public EmbeddedSubscriber(IPublisher publisher, IAuthenticationProvider authenticationProvider, ILogger log,
			Guid connectionId) {
			_publisher = publisher;
			_authenticationProvider = authenticationProvider;
			_log = log;
			_connectionId = connectionId;
			_subscriptions = new EmbeddedSubcriptionsManager();
		}

		public void Handle(ClientMessage.StreamEventAppeared message) {
			IEmbeddedSubscription subscription;
			_subscriptions.TryGetActiveSubscription(message.CorrelationId, out subscription);
			((EmbeddedSubscription)subscription).EventAppeared(message.Event);
		}
		
		public void Handle(ClientMessage.CheckpointReached message) {
			_subscriptions.TryGetActiveSubscription(message.CorrelationId, out var subscription);
			((FilteredEmbeddedSubscription)subscription).CheckpointReached(message.Position);
		}

		public void Handle(ClientMessage.SubscriptionConfirmation message) {
			ConfirmSubscription(message.CorrelationId, message.LastCommitPosition, message.LastEventNumber);
		}

		public void Handle(ClientMessage.SubscriptionDropped message) {
			IEmbeddedSubscription subscription;
			_subscriptions.TryGetActiveSubscription(message.CorrelationId, out subscription);
			subscription.DropSubscription(message.Reason);
		}

		public void Handle(ClientMessage.PersistentSubscriptionConfirmation message) {
			ConfirmSubscription(message.SubscriptionId, message.CorrelationId, message.LastCommitPosition,
				message.LastEventNumber);
		}

		public void Handle(ClientMessage.PersistentSubscriptionStreamEventAppeared message) {
			IEmbeddedSubscription subscription;
			_subscriptions.TryGetActiveSubscription(message.CorrelationId, out subscription);
			((EmbeddedPersistentSubscription)subscription).EventAppeared(message.Event, message.RetryCount);
		}

		private void ConfirmSubscription(Guid correlationId, long lastCommitPosition, long? lastEventNumber) {
			IEmbeddedSubscription subscription;
			_subscriptions.TryGetActiveSubscription(correlationId, out subscription);
			subscription.ConfirmSubscription(lastCommitPosition, lastEventNumber);
		}

		private void ConfirmSubscription(string subscriptionId, Guid correlationId, long lastCommitPosition,
			long? lastEventNumber) {
			IEmbeddedSubscription subscription;
			_subscriptions.TryGetActiveSubscription(correlationId, out subscription);
			((EmbeddedPersistentSubscription)subscription).UpdateSubscriptionId(subscriptionId);
			subscription.ConfirmSubscription(lastCommitPosition, lastEventNumber);
		}

		public void StartSubscription(Guid correlationId, TaskCompletionSource<EventStoreSubscription> source,
			string stream, UserCredentials userCredentials, bool resolveLinkTos,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped) {
			var subscription = new EmbeddedSubscription(
				_log, _publisher, _connectionId, source, stream, userCredentials, _authenticationProvider,
				resolveLinkTos, eventAppeared,
				subscriptionDropped);

			_subscriptions.StartSubscription(correlationId, subscription);
		}
		
		public void StartFilteredSubscription(Guid correlationId, TaskCompletionSource<EventStoreSubscription> source,
			string stream, UserCredentials userCredentials, bool resolveLinkTos, TcpClientMessageDto.Filter filter,
			Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
			Func<EventStoreSubscription, Position, Task> checkpointReached, int checkpointInterval,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped) {
			var subscription = new FilteredEmbeddedSubscription(
				_log, _publisher, _connectionId, source, stream, userCredentials, _authenticationProvider,
				resolveLinkTos, filter, eventAppeared, checkpointReached, checkpointInterval,
				subscriptionDropped);

			_subscriptions.StartSubscription(correlationId, subscription);
		}

		public void StartPersistentSubscription(Guid correlationId,
			TaskCompletionSource<PersistentEventStoreSubscription> source, string subscriptionId, string streamId,
			UserCredentials userCredentials, int bufferSize,
			Func<EventStoreSubscription, PersistentSubscriptionResolvedEvent, Task> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped, int maxRetries,
			TimeSpan operationTimeout) {
			var subscription = new EmbeddedPersistentSubscription(_log, _publisher, _connectionId, source,
				subscriptionId, streamId, userCredentials, _authenticationProvider, bufferSize, eventAppeared,
				subscriptionDropped, maxRetries, operationTimeout);

			_subscriptions.StartSubscription(correlationId, subscription);
		}
	}
}
