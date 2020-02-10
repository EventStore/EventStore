using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client.Streams;

namespace EventStore.Client {
	public partial class EventStoreClient {
		private StreamSubscription SubscribeToAll(
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			EventStoreClientOperationOptions operationOptions,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			operationOptions.TimeoutAfter = DeadLine.None;

			var subscription = new StreamSubscription(ReadInternal(new ReadReq {
					Options = new ReadReq.Types.Options {
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
						ResolveLinks = resolveLinkTos,
						All = ReadReq.Types.Options.Types.AllOptions.FromPosition(Position.Start),
						Subscription = new ReadReq.Types.Options.Types.SubscriptionOptions(),
						Filter = GetFilterOptions(filter)
					}
				}, operationOptions, userCredentials, cancellationToken), eventAppeared,
				subscriptionDropped);
			return subscription;
		}

		/// <summary>
		/// Subscribes to all events. Use this when you have no checkpoint.
		/// </summary>
		/// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
		/// <param name="filter">The optional <see cref="IEventFilter"/> to apply.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public StreamSubscription SubscribeToAll(
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => SubscribeToAll(eventAppeared,
			_settings.OperationOptions.Clone(), resolveLinkTos, subscriptionDropped, filter, userCredentials,
			cancellationToken);

		/// <summary>
		/// Subscribes to all events. Use this when you have no checkpoint.
		/// </summary>
		/// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
		/// <param name="filter">The optional <see cref="IEventFilter"/> to apply.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public StreamSubscription SubscribeToAll(
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions(operationOptions);

			return SubscribeToAll(eventAppeared, operationOptions, resolveLinkTos, subscriptionDropped, filter,
				userCredentials,
				cancellationToken);
		}

		private StreamSubscription SubscribeToAll(Position start,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			EventStoreClientOperationOptions operationOptions,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			operationOptions.TimeoutAfter = DeadLine.None;

			var subscription = new StreamSubscription(ReadInternal(new ReadReq {
					Options = new ReadReq.Types.Options {
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
						ResolveLinks = resolveLinkTos,
						All = ReadReq.Types.Options.Types.AllOptions.FromPosition(start),
						Subscription = new ReadReq.Types.Options.Types.SubscriptionOptions(),
						Filter = GetFilterOptions(filter)
					}
				}, operationOptions, userCredentials, cancellationToken), eventAppeared,
				subscriptionDropped);
			return subscription;
		}

		/// <summary>
		/// Subscribes to all events from a checkpoint. This is exclusive of.
		/// </summary>
		/// <param name="start">A position (exclusive of) to start the subscription from.</param>
		/// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
		/// <param name="filter">The optional <see cref="IEventFilter"/> to apply.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public StreamSubscription SubscribeToAll(Position start,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => SubscribeToAll(start, eventAppeared,
			_settings.OperationOptions.Clone(), resolveLinkTos, subscriptionDropped, filter, userCredentials,
			cancellationToken);

		/// <summary>
		/// Subscribes to all events from a checkpoint. This is exclusive of.
		/// </summary>
		/// <param name="start">A <see cref="Position"/> (exclusive of) to start the subscription from.</param>
		/// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
		/// <param name="filter">The optional <see cref="IEventFilter"/> to apply.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public StreamSubscription SubscribeToAll(Position start,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			IEventFilter filter = null,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions(operationOptions);

			return SubscribeToAll(start, eventAppeared, operationOptions, resolveLinkTos, subscriptionDropped, filter,
				userCredentials, cancellationToken);
		}

		private StreamSubscription SubscribeToStream(string streamName,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			EventStoreClientOperationOptions operationOptions,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			operationOptions.TimeoutAfter = DeadLine.None;

			var subscription = new StreamSubscription(ReadInternal(new ReadReq {
					Options = new ReadReq.Types.Options {
						ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
						ResolveLinks = resolveLinkTos,
						Stream = ReadReq.Types.Options.Types.StreamOptions.FromStreamNameAndRevision(
							streamName,
							StreamRevision.Start),
						Subscription = new ReadReq.Types.Options.Types.SubscriptionOptions()
					}
				}, operationOptions, userCredentials, cancellationToken), eventAppeared,
				subscriptionDropped);
			return subscription;
		}

		/// <summary>
		/// Subscribes to a stream from a checkpoint. This is exclusive of.
		/// </summary>
		/// <param name="streamName">The name of the stream to read events from.</param>
		/// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public StreamSubscription SubscribeToStream(string streamName,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => SubscribeToStream(streamName, eventAppeared,
			_settings.OperationOptions.Clone(), resolveLinkTos, subscriptionDropped,
			userCredentials, cancellationToken);

		/// <summary>
		/// Subscribes to a stream from a checkpoint. This is exclusive of.
		/// </summary>
		/// <param name="streamName">The name of the stream to read events from.</param>
		/// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public StreamSubscription SubscribeToStream(string streamName,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			
			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions(operationOptions);
			
			return SubscribeToStream(streamName, eventAppeared, operationOptions, resolveLinkTos, subscriptionDropped,
				userCredentials, cancellationToken);
		}

		private StreamSubscription SubscribeToStream(string streamName,
			StreamRevision start,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			EventStoreClientOperationOptions operationOptions,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			
			operationOptions.TimeoutAfter = DeadLine.None;

			var subscription = new StreamSubscription(ReadInternal(new ReadReq {
						Options = new ReadReq.Types.Options {
							ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
							ResolveLinks = resolveLinkTos,
							Stream = ReadReq.Types.Options.Types.StreamOptions.FromStreamNameAndRevision(streamName,
								start),
							Subscription = new ReadReq.Types.Options.Types.SubscriptionOptions()
						}
					},
					operationOptions, userCredentials, cancellationToken), eventAppeared,
				subscriptionDropped);
			return subscription;
		}

		/// <summary>
		/// Subscribes to a stream from a checkpoint. This is exclusive of.
		/// </summary>
		/// <param name="start">A <see cref="Position"/> (exclusive of) to start the subscription from.</param>
		/// <param name="streamName">The name of the stream to read events from.</param>
		/// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public StreamSubscription SubscribeToStream(string streamName,
			StreamRevision start,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => SubscribeToStream(streamName, start, eventAppeared,
			_settings.OperationOptions.Clone(), resolveLinkTos, subscriptionDropped,
			userCredentials, cancellationToken);

		/// <summary>
		/// Subscribes to a stream from a checkpoint. This is exclusive of.
		/// </summary>
		/// <param name="start">A <see cref="Position"/> (exclusive of) to start the subscription from.</param>
		/// <param name="streamName">The name of the stream to read events from.</param>
		/// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
		/// <param name="configureOperationOptions">An <see cref="Action{EventStoreClientOperationOptions}"/> to configure the operation's options.</param>
		/// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
		/// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
		/// <param name="userCredentials">The optional user credentials to perform operation with.</param>
		/// <param name="cancellationToken">The optional <see cref="System.Threading.CancellationToken"/>.</param>
		/// <returns></returns>
		public StreamSubscription SubscribeToStream(string streamName,
			StreamRevision start,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Action<EventStoreClientOperationOptions> configureOperationOptions,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) {
			
			var operationOptions = _settings.OperationOptions.Clone();
			configureOperationOptions(operationOptions);
			
			return SubscribeToStream(streamName, start, eventAppeared, operationOptions, resolveLinkTos,
				subscriptionDropped,
				userCredentials, cancellationToken);
		}
	}
}
