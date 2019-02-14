using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
#if!NET452
using TaskEx = System.Threading.Tasks.Task;

#else
using EventStore.ClientAPI.Common.Utils.Threading;
#endif
namespace EventStore.ClientAPI {
	/// <summary>
	/// Extensions for <seealso cref="IEventStoreConnection"/>
	/// </summary>
	public static class IEventStoreConnectionExtensions {
		private static Func<TConnection, ResolvedEvent, Task> ToTask<TConnection>(
			Action<TConnection, ResolvedEvent> eventAppeared) =>
			(subscription, e) => {
				eventAppeared(subscription, e);
				return TaskEx.CompletedTask;
			};

		/// <summary>
		/// Asynchronously subscribes to a single event stream. New events
		/// written to the stream while the subscription is active will be
		/// pushed to the client.
		/// </summary>
		/// <param name="target">The connection to subscribe to</param>
		/// <param name="stream">The stream to subscribe to</param>
		/// <param name="resolveLinkTos">Whether to resolve Link events automatically</param>
		/// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
		/// <param name="userCredentials">User credentials to use for the operation</param>
		/// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
		public static Task<EventStoreSubscription> SubscribeToStreamAsync(
			this IEventStoreConnection target,
			string stream,
			bool resolveLinkTos,
			Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) =>
			target.SubscribeToStreamAsync(
				stream,
				resolveLinkTos,
				ToTask(eventAppeared),
				subscriptionDropped,
				userCredentials
			);

		/// <summary>
		/// Subscribes to a single event stream. Existing events from
		/// lastCheckpoint onwards are read from the stream
		/// and presented to the user of <see cref="EventStoreCatchUpSubscription"/>
		/// as if they had been pushed.
		///
		/// Once the end of the stream is read the subscription is
		/// transparently (to the user) switched to push new events as
		/// they are written.
		///
		/// The action liveProcessingStarted is called when the
		/// <see cref="EventStoreCatchUpSubscription"/> switches from the reading
		/// phase to the live subscription phase.
		/// </summary>
		/// <param name="stream">The stream to subscribe to</param>
		/// <param name="lastCheckpoint">The event number from which to start.
		///
		/// To receive all events in the stream, use <see cref="StreamCheckpoint.StreamStart" />.
		/// If events have already been received and resubscription from the same point
		/// is desired, use the event number of the last event processed which
		/// appeared on the subscription.
		///
		/// NOTE: Using <see cref="StreamPosition.Start" /> here will result in missing
		/// the first event in the stream.</param>
		/// <param name="target">The connection to subscribe to</param>
		/// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
		/// <param name="liveProcessingStarted">An action invoked when the subscription switches to newly-pushed events</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
		/// <param name="userCredentials">User credentials to use for the operation</param>
		/// <param name="settings">The <see cref="CatchUpSubscriptionSettings"/> for the subscription</param>
		/// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
		public static EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
			this IEventStoreConnection target,
			string stream,
			long? lastCheckpoint,
			CatchUpSubscriptionSettings settings,
			Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) =>
			target.SubscribeToStreamFrom(
				stream,
				lastCheckpoint,
				settings,
				ToTask(eventAppeared),
				liveProcessingStarted,
				subscriptionDropped,
				userCredentials
			);

		/// <summary>
		/// Asynchronously subscribes to all events in the Event Store. New
		/// events written to the stream while the subscription is active
		/// will be pushed to the client.
		/// </summary>
		/// <param name="target">The connection to subscribe to</param>
		/// <param name="resolveLinkTos">Whether to resolve Link events automatically</param>
		/// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
		/// <param name="userCredentials">User credentials to use for the operation</param>
		/// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
		public static Task<EventStoreSubscription> SubscribeToAllAsync(
			this IEventStoreConnection target,
			bool resolveLinkTos,
			Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) =>
			target.SubscribeToAllAsync(
				resolveLinkTos,
				ToTask(eventAppeared),
				subscriptionDropped,
				userCredentials
			);

		/// <summary>
		/// Subscribes to a persistent subscription(competing consumer) on event store
		/// </summary>
		/// <param name="target">The connection to subscribe to</param>
		/// <param name="groupName">The subscription group to connect to</param>
		/// <param name="stream">The stream to subscribe to</param>
		/// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
		/// <param name="userCredentials">User credentials to use for the operation</param>
		/// <param name="bufferSize">The buffer size to use for the persistent subscription</param>
		/// <param name="autoAck">Whether the subscription should automatically acknowledge messages processed.
		/// If not set the receiver is required to explicitly acknowledge messages through the subscription.</param>
		/// <remarks>This will connect you to a persistent subscription group for a stream. The subscription group
		/// must first be created with CreatePersistentSubscriptionAsync many connections
		/// can connect to the same group and they will be treated as competing consumers within the group.
		/// If one connection dies work will be balanced across the rest of the consumers in the group. If
		/// you attempt to connect to a group that does not exist you will be given an exception.
		/// </remarks>
		/// <returns>An <see cref="EventStorePersistentSubscriptionBase"/> representing the subscription</returns>
		public static EventStorePersistentSubscriptionBase ConnectToPersistentSubscription(
			this IEventStoreConnection target,
			string stream,
			string groupName,
			Action<EventStorePersistentSubscriptionBase, ResolvedEvent> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null,
			int bufferSize = 10,
			bool autoAck = true) =>
			target.ConnectToPersistentSubscription(
				stream,
				groupName,
				ToTask(eventAppeared),
				subscriptionDropped,
				userCredentials,
				bufferSize,
				autoAck
			);

		/// <summary>
		/// Subscribes to a persistent subscription(competing consumer) on event store
		/// </summary>
		/// <param name="target">The connection to subscribe to</param>
		/// <param name="groupName">The subscription group to connect to</param>
		/// <param name="stream">The stream to subscribe to</param>
		/// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
		/// <param name="userCredentials">User credentials to use for the operation</param>
		/// <param name="bufferSize">The buffer size to use for the persistent subscription</param>
		/// <param name="autoAck">Whether the subscription should automatically acknowledge messages processed.
		/// If not set the receiver is required to explicitly acknowledge messages through the subscription.</param>
		/// <remarks>This will connect you to a persistent subscription group for a stream. The subscription group
		/// must first be created with CreatePersistentSubscriptionAsync many connections
		/// can connect to the same group and they will be treated as competing consumers within the group.
		/// If one connection dies work will be balanced across the rest of the consumers in the group. If
		/// you attempt to connect to a group that does not exist you will be given an exception.
		/// </remarks>
		/// <returns>An <see cref="EventStorePersistentSubscriptionBase"/> representing the subscription</returns>
		public static EventStorePersistentSubscriptionBase ConnectToPersistentSubscription(
			this IEventStoreConnection target,
			string stream,
			string groupName,
			Func<EventStorePersistentSubscriptionBase, ResolvedEvent, Task> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null,
			int bufferSize = 10,
			bool autoAck = true) =>
			target.ConnectToPersistentSubscription(
				stream,
				groupName,
				(s, e, i) => eventAppeared(s, e),
				subscriptionDropped,
				userCredentials,
				bufferSize,
				autoAck
			);

		/// <summary>
		/// Subscribes to a persistent subscription(competing consumer) on event store
		/// </summary>
		/// <param name="target">The connection to subscribe to</param>
		/// <param name="groupName">The subscription group to connect to</param>
		/// <param name="stream">The stream to subscribe to</param>
		/// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
		/// <param name="userCredentials">User credentials to use for the operation</param>
		/// <param name="bufferSize">The buffer size to use for the persistent subscription</param>
		/// <param name="autoAck">Whether the subscription should automatically acknowledge messages processed.
		/// If not set the receiver is required to explicitly acknowledge messages through the subscription.</param>
		/// <remarks>This will connect you to a persistent subscription group for a stream. The subscription group
		/// must first be created with CreatePersistentSubscriptionAsync many connections
		/// can connect to the same group and they will be treated as competing consumers within the group.
		/// If one connection dies work will be balanced across the rest of the consumers in the group. If
		/// you attempt to connect to a group that does not exist you will be given an exception.
		/// </remarks>
		/// <returns>An <see cref="EventStorePersistentSubscriptionBase"/> representing the subscription</returns>
		public static Task<EventStorePersistentSubscriptionBase> ConnectToPersistentSubscriptionAsync(
			this IEventStoreConnection target,
			string stream,
			string groupName,
			Action<EventStorePersistentSubscriptionBase, ResolvedEvent> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null,
			int bufferSize = 10,
			bool autoAck = true) =>
			target.ConnectToPersistentSubscriptionAsync(
				stream,
				groupName,
				ToTask(eventAppeared),
				subscriptionDropped,
				userCredentials,
				bufferSize,
				autoAck
			);

		/// <summary>
		/// Subscribes to a persistent subscription(competing consumer) on event store
		/// </summary>
		/// <param name="target">The connection to subscribe to</param>
		/// <param name="groupName">The subscription group to connect to</param>
		/// <param name="stream">The stream to subscribe to</param>
		/// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
		/// <param name="userCredentials">User credentials to use for the operation</param>
		/// <param name="bufferSize">The buffer size to use for the persistent subscription</param>
		/// <param name="autoAck">Whether the subscription should automatically acknowledge messages processed.
		/// If not set the receiver is required to explicitly acknowledge messages through the subscription.</param>
		/// <remarks>This will connect you to a persistent subscription group for a stream. The subscription group
		/// must first be created with CreatePersistentSubscriptionAsync many connections
		/// can connect to the same group and they will be treated as competing consumers within the group.
		/// If one connection dies work will be balanced across the rest of the consumers in the group. If
		/// you attempt to connect to a group that does not exist you will be given an exception.
		/// </remarks>
		/// <returns>An <see cref="EventStorePersistentSubscriptionBase"/> representing the subscription</returns>
		public static Task<EventStorePersistentSubscriptionBase> ConnectToPersistentSubscriptionAsync(
			this IEventStoreConnection target,
			string stream,
			string groupName,
			Func<EventStorePersistentSubscriptionBase, ResolvedEvent, Task> eventAppeared,
			Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null,
			int bufferSize = 10,
			bool autoAck = true) =>
			target.ConnectToPersistentSubscriptionAsync(
				stream,
				groupName,
				(s, e, i) => eventAppeared(s, e),
				subscriptionDropped,
				userCredentials,
				bufferSize,
				autoAck
			);

		/// <summary>
		/// Subscribes to a all events. Existing events from lastCheckpoint
		/// onwards are read from the Event Store and presented to the user of
		/// <see cref="EventStoreCatchUpSubscription"/> as if they had been pushed.
		///
		/// Once the end of the stream is read the subscription is
		/// transparently (to the user) switched to push new events as
		/// they are written.
		///
		/// The action liveProcessingStarted is called when the
		/// <see cref="EventStoreCatchUpSubscription"/> switches from the reading
		/// phase to the live subscription phase.
		/// </summary>
		/// <param name="lastCheckpoint">The position from which to start.
		///
		/// To receive all events in the database, use <see cref="AllCheckpoint.AllStart" />.
		/// If events have already been received and resubscription from the same point
		/// is desired, use the position representing the last event processed which
		/// appeared on the subscription.
		///
		/// NOTE: Using <see cref="Position.Start" /> here will result in missing
		/// the first event in the stream.</param>
		/// <param name="target">The connection to subscribe to</param>
		/// <param name="eventAppeared">An action invoked when a new event is received over the subscription</param>
		/// <param name="liveProcessingStarted">An action invoked when the subscription switches to newly-pushed events</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped</param>
		/// <param name="userCredentials">User credentials to use for the operation</param>
		/// <param name="settings">The <see cref="CatchUpSubscriptionSettings"/> for the subscription</param>
		/// <returns>An <see cref="EventStoreSubscription"/> representing the subscription</returns>
		public static EventStoreAllCatchUpSubscription SubscribeToAllFrom(
			this IEventStoreConnection target,
			Position? lastCheckpoint,
			CatchUpSubscriptionSettings settings,
			Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
			UserCredentials userCredentials = null) =>
			target.SubscribeToAllFrom(
				lastCheckpoint,
				settings,
				ToTask(eventAppeared),
				liveProcessingStarted,
				subscriptionDropped,
				userCredentials
			);
	}
}
