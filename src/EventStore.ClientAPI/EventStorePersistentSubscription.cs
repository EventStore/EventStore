using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.ClientOperations;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.Internal;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI {
	internal struct PersistentSubscriptionResolvedEvent : IResolvedEvent {
		/// <summary>
		/// 
		/// </summary>
		public readonly int? RetryCount;

		/// <summary>
		/// 
		/// </summary>
		public readonly ResolvedEvent Event;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="event"></param>
		/// <param name="retryCount"></param>
		internal PersistentSubscriptionResolvedEvent(ResolvedEvent @event, int? retryCount) {
			Event = @event;
			RetryCount = retryCount;
		}

		string IResolvedEvent.OriginalStreamId => Event.OriginalStreamId;

		long IResolvedEvent.OriginalEventNumber => Event.OriginalEventNumber;

		RecordedEvent IResolvedEvent.OriginalEvent => Event.OriginalEvent;

		Position? IResolvedEvent.OriginalPosition => Event.OriginalPosition;

		/// <summary>
		/// 
		/// </summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public static implicit operator ResolvedEvent(PersistentSubscriptionResolvedEvent x) => x.Event;
	}

	/// <summary>
	/// Represents a persistent subscription connection.
	/// </summary>
	public class EventStorePersistentSubscription : EventStorePersistentSubscriptionBase {
		private readonly EventStoreConnectionLogicHandler _handler;

		internal EventStorePersistentSubscription(
			string subscriptionId, string streamId,
			Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped,
			UserCredentials userCredentials, ILogger log, bool verboseLogging, ConnectionSettings settings,
			EventStoreConnectionLogicHandler handler, int bufferSize = 10, bool autoAck = true)
			: base(
				subscriptionId, streamId, eventAppeared, subscriptionDropped, userCredentials, log, verboseLogging,
				settings, bufferSize, autoAck) {
			_handler = handler;
		}

		internal override Task<PersistentEventStoreSubscription> StartSubscription(
			string subscriptionId, string streamId, int bufferSize, UserCredentials userCredentials,
			Func<EventStoreSubscription, PersistentSubscriptionResolvedEvent, Task> onEventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> onSubscriptionDropped,
			ConnectionSettings settings) {
			var source = TaskCompletionSourceFactory.Create<PersistentEventStoreSubscription>();
			_handler.EnqueueMessage(new StartPersistentSubscriptionMessage(source, subscriptionId, streamId, bufferSize,
				userCredentials, onEventAppeared,
				onSubscriptionDropped, settings.MaxRetries, settings.OperationTimeout));

			return source.Task;
		}
	}
}
