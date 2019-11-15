using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
#if!NET452
using TaskEx = System.Threading.Tasks.Task;

#endif

namespace EventStore.ClientAPI {
	/// <summary>
	/// A catch-up subscription to a single stream in the Event Store.
	/// </summary>
	public class EventStoreStreamCatchUpSubscription : EventStoreCatchUpSubscription {
		/// <summary>
		/// The last event number processed on the subscription.
		/// </summary>
		public long LastProcessedEventNumber {
			get { return _lastProcessedEventNumber; }
		}

		private long _nextReadEventNumber;
		private long _lastProcessedEventNumber;

		internal EventStoreStreamCatchUpSubscription(IEventStoreConnection connection,
			ILogger log,
			string streamId,
			long? fromEventNumberExclusive, /* if null -- from the very beginning */
			UserCredentials userCredentials,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
			CatchUpSubscriptionSettings settings)
			: base(connection, log, streamId, userCredentials,
				eventAppeared, liveProcessingStarted, subscriptionDropped, settings) {
			Ensure.NotNullOrEmpty(streamId, "streamId");

			_lastProcessedEventNumber = fromEventNumberExclusive ?? -1;
			_nextReadEventNumber = fromEventNumberExclusive ?? 0;
		}

		/// <summary>
		/// Read events until the given event number async.
		/// </summary>
		/// <param name="connection">The connection.</param>
		/// <param name="resolveLinkTos">Whether to resolve Link events.</param>
		/// <param name="userCredentials">User credentials for the operation.</param>
		/// <param name="lastCommitPosition">The commit position to read until.</param>
		/// <param name="lastEventNumber">The event number to read until.</param>
		/// <returns></returns>
		protected override Task<Position> ReadEventsTillAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials,
			long? lastCommitPosition, long? lastEventNumber) =>
			ReadEventsInternalAsync(connection, resolveLinkTos, userCredentials, lastEventNumber);

		private async Task<Position> ReadEventsInternalAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials, long? lastEventNumber) {
			bool shouldStopOrDone;
			do {
				var slice = await connection
					.ReadStreamEventsForwardAsync(StreamId, _nextReadEventNumber, ReadBatchSize, resolveLinkTos,
						userCredentials).ConfigureAwait(false);
				shouldStopOrDone = await ReadEventsCallbackAsync(slice, lastEventNumber).ConfigureAwait(false);
			} while (!shouldStopOrDone);

			return Position.Start;
		}

		private async Task<bool> ReadEventsCallbackAsync(StreamEventsSlice slice, long? lastEventNumber) {
			bool shouldStopOrDone =
				ShouldStop || await ProcessEventsAsync(lastEventNumber, slice).ConfigureAwait(false);
			if (shouldStopOrDone && Verbose) {
				Log.Debug(
					"Catch-up Subscription {0} to {1}: finished reading events, nextReadEventNumber = {2}.",
					SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId,
					_nextReadEventNumber);
			}

			return shouldStopOrDone;
		}

		private async Task<bool> ProcessEventsAsync(long? lastEventNumber, StreamEventsSlice slice) {
			bool done;
			switch (slice.Status) {
				case SliceReadStatus.Success: {
					foreach (var e in slice.Events) {
						await TryProcessAsync(e).ConfigureAwait(false);
					}

					_nextReadEventNumber = slice.NextEventNumber;
					done = lastEventNumber == null ? slice.IsEndOfStream : slice.NextEventNumber > lastEventNumber;
					break;
				}
				case SliceReadStatus.StreamNotFound: {
					if (lastEventNumber.HasValue && lastEventNumber != -1) {
						throw new Exception(
							string.Format(
								"Impossible: stream {0} disappeared in the middle of catching up subscription {1}.",
								StreamId, SubscriptionName));
					}

					done = true;
					break;
				}
				case SliceReadStatus.StreamDeleted:
					throw new StreamDeletedException(StreamId);
				default:
					throw new ArgumentOutOfRangeException(string.Format(
						"Subscription {0} unexpected StreamEventsSlice.Status: {0}.",
						SubscriptionName, slice.Status));
			}

			if (!done && slice.IsEndOfStream)
				await Task.Delay(1).ConfigureAwait(false); // we are awaiting the server to flush its data
			return done;
		}

		/// <summary>
		/// Try to process a single <see cref="ResolvedEvent"/>.
		/// </summary>
		/// <param name="e">The <see cref="ResolvedEvent"/> to process.</param>
		protected override async Task TryProcessAsync(ResolvedEvent e) {
			bool processed = false;
			if (e.OriginalEventNumber > _lastProcessedEventNumber) {
				try {
					await EventAppeared(this, e).ConfigureAwait(false);
				} catch (Exception ex) {
					DropSubscription(SubscriptionDropReason.EventHandlerException, ex);
					throw;
				}

				_lastProcessedEventNumber = e.OriginalEventNumber;
				processed = true;
			}

			if (Verbose) {
				Log.Debug("Catch-up Subscription {0} to {1}: {2} event ({3}, {4}, {5} @ {6}).",
					SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId, processed ? "processed" : "skipping",
					e.OriginalEvent.EventStreamId, e.OriginalEvent.EventNumber, e.OriginalEvent.EventType,
					e.OriginalEventNumber);
			}
		}

		protected override async Task SubscribeToStreamAsync() {
			if (!ShouldStop) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to {1}: subscribing...", SubscriptionName, StreamId);

				var subscription = await Connection.SubscribeToStreamAsync(StreamId, ResolveLinkTos, EnqueuePushedEvent,
					ServerSubscriptionDropped, UserCredentials).ConfigureAwait(false);

				Subscription = subscription;
				await ReadMissedHistoricEventsAsync().ConfigureAwait(false);
			} else {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
			}
		}

		protected override Task LiveProcessingStarted(EventStoreCatchUpSubscription eventStoreCatchUpSubscription,
			Position lastPosition) {
			return TaskEx.CompletedTask;
		}
	}
}
