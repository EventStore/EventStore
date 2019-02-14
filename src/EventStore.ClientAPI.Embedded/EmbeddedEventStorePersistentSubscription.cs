using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.Embedded {
	internal class EmbeddedEventStorePersistentSubscription : EventStorePersistentSubscriptionBase {
		private readonly EmbeddedSubscriber _subscriptions;

		internal EmbeddedEventStorePersistentSubscription(
			string subscriptionId, string streamId,
			Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped,
			UserCredentials userCredentials, ILogger log, bool verboseLogging, ConnectionSettings settings,
			EmbeddedSubscriber subscriptions,
			int bufferSize = 10, bool autoAck = true)
			: base(
				subscriptionId, streamId, eventAppeared, subscriptionDropped, userCredentials, log, verboseLogging,
				settings, bufferSize, autoAck) {
			_subscriptions = subscriptions;
		}

		internal override Task<PersistentEventStoreSubscription> StartSubscription(
			string subscriptionId, string streamId, int bufferSize, UserCredentials userCredentials,
			Func<EventStoreSubscription, PersistentSubscriptionResolvedEvent, Task> onEventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> onSubscriptionDropped,
			ConnectionSettings settings) {
			var source =
				new TaskCompletionSource<PersistentEventStoreSubscription>(TaskCreationOptions
					.RunContinuationsAsynchronously);

			_subscriptions.StartPersistentSubscription(Guid.NewGuid(), source, subscriptionId, streamId,
				userCredentials, bufferSize, onEventAppeared,
				onSubscriptionDropped, settings.MaxRetries, settings.OperationTimeout);

			return source.Task;
		}
	}
}
