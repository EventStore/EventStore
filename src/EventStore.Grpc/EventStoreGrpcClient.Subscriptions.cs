using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Grpc.Streams;

namespace EventStore.Grpc {
	public partial class EventStoreGrpcClient {
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
			CancellationToken cancellationToken = default) => new StreamSubscription(ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
					ResolveLinks = resolveLinkTos,
					All = new ReadReq.Types.Options.Types.AllOptions {
						Start = new ReadReq.Types.Empty()
					},
					Subscription = new ReadReq.Types.Options.Types.SubscriptionOptions(),
					Filter = GetFilterOptions(filter)
				}
			}, userCredentials,
			cancellationToken), eventAppeared, subscriptionDropped);

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
			CancellationToken cancellationToken = default) => new StreamSubscription(ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
					ResolveLinks = resolveLinkTos,
					All = new ReadReq.Types.Options.Types.AllOptions {
						Position = new ReadReq.Types.Options.Types.Position {
							CommitPosition = start.CommitPosition,
							PreparePosition = start.PreparePosition
						}
					},
					Subscription = new ReadReq.Types.Options.Types.SubscriptionOptions(),
					Filter = GetFilterOptions(filter)
				}
			}, userCredentials,
			cancellationToken), eventAppeared, subscriptionDropped);

		public StreamSubscription SubscribeToStream(string streamName,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => new StreamSubscription(ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
					ResolveLinks = resolveLinkTos,
					Stream = new ReadReq.Types.Options.Types.StreamOptions {
						StreamName = streamName,
						Start = new ReadReq.Types.Empty()
					},
					Subscription = new ReadReq.Types.Options.Types.SubscriptionOptions()
				}
			},
			userCredentials,
			cancellationToken), eventAppeared, subscriptionDropped);

		public StreamSubscription SubscribeToStream(string streamName,
			StreamRevision start,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			bool resolveLinkTos = false,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			UserCredentials userCredentials = default,
			CancellationToken cancellationToken = default) => new StreamSubscription(ReadInternal(new ReadReq {
				Options = new ReadReq.Types.Options {
					ReadDirection = ReadReq.Types.Options.Types.ReadDirection.Forwards,
					ResolveLinks = resolveLinkTos,
					Stream = new ReadReq.Types.Options.Types.StreamOptions {
						StreamName = streamName,
						Revision = start
					},
					Subscription = new ReadReq.Types.Options.Types.SubscriptionOptions()
				}
			},
			userCredentials,
			cancellationToken), eventAppeared, subscriptionDropped);
	}
}
