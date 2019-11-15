using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using TaskEx = System.Threading.Tasks.Task;

namespace EventStore.ClientAPI
{
	/// <summary>
	/// A catch-up subscription to all events in the Event Store.
	/// </summary>
	public class EventStoreAllCatchUpSubscription : EventStoreCatchUpSubscription {
		/// <summary>
		/// The last position processed on the subscription.
		/// </summary>
		public Position LastProcessedPosition {
			get {
				Position oldPos = _lastProcessedPosition;
				Position curPos;
				while (oldPos != (curPos = _lastProcessedPosition)) {
					oldPos = curPos;
				}

				return curPos;
			}
		}

		private Position _nextReadPosition;
		private Position _lastProcessedPosition;

		internal EventStoreAllCatchUpSubscription(IEventStoreConnection connection,
			ILogger log,
			Position? fromPositionExclusive, /* if null -- from the very beginning */
			UserCredentials userCredentials,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
			CatchUpSubscriptionSettings settings)
			: base(connection, log, string.Empty, userCredentials,
				eventAppeared, liveProcessingStarted, subscriptionDropped, settings) {
			_lastProcessedPosition = fromPositionExclusive ?? new Position(-1, -1);
			_nextReadPosition = fromPositionExclusive ?? Position.Start;
		}

		/// <summary>
		/// Read events until the given position async.
		/// </summary>
		/// <param name="connection">The connection.</param>
		/// <param name="resolveLinkTos">Whether to resolve Link events.</param>
		/// <param name="userCredentials">User credentials for the operation.</param>
		/// <param name="lastCommitPosition">The commit position to read until.</param>
		/// <param name="lastEventNumber">The event number to read until.</param>
		/// <returns></returns>
		protected override Task<Position> ReadEventsTillAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials, long? lastCommitPosition, long? lastEventNumber) =>
			ReadEventsInternalAsync(connection, resolveLinkTos, userCredentials, lastCommitPosition);

		private async Task<Position> ReadEventsInternalAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials, long? lastCommitPosition) {
			bool shouldStopOrDone;
			AllEventsSlice slice;
			do {
				slice = await connection
					.ReadAllEventsForwardAsync(_nextReadPosition, ReadBatchSize, resolveLinkTos, userCredentials)
					.ConfigureAwait(false);
				shouldStopOrDone = await ReadEventsCallbackAsync(slice, lastCommitPosition).ConfigureAwait(false);
			} while (!shouldStopOrDone);

			return slice.NextPosition;
		}

		private async Task<bool> ReadEventsCallbackAsync(AllEventsSlice slice, long? lastCommitPosition) {
			bool shouldStopOrDone =
				ShouldStop || await ProcessEventsAsync(lastCommitPosition, slice).ConfigureAwait(false);
			if (shouldStopOrDone && Verbose) {
				Log.Debug(
					"Catch-up Subscription {0} to {1}: finished reading events, nextReadPosition = {2}.",
					SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId,
					_nextReadPosition);
			}

			return shouldStopOrDone;
		}

		private async Task<bool> ProcessEventsAsync(long? lastCommitPosition, AllEventsSlice slice) {
			foreach (var e in slice.Events) {
				if (e.OriginalPosition == null)
					throw new Exception(String.Format("Subscription {0} event came up with no OriginalPosition.",
						SubscriptionName));
				await TryProcessAsync(e).ConfigureAwait(false);
			}

			_nextReadPosition = slice.NextPosition;

			var done = lastCommitPosition == null
				? slice.IsEndOfStream
				: slice.NextPosition >= new Position(lastCommitPosition.Value, lastCommitPosition.Value);

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
			if (e.OriginalPosition > _lastProcessedPosition) {
				try {
					await EventAppeared(this, e).ConfigureAwait(false);
				} catch (Exception ex) {
					DropSubscription(SubscriptionDropReason.EventHandlerException, ex);
					throw;
				}

				_lastProcessedPosition = e.OriginalPosition.Value;
				processed = true;
			}

			if (Verbose) {
				Log.Debug("Catch-up Subscription {0} to {1}: {2} event ({3}, {4}, {5} @ {6}).",
					SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId,
					processed ? "processed" : "skipping",
					e.OriginalEvent.EventStreamId, e.OriginalEvent.EventNumber, e.OriginalEvent.EventType,
					e.OriginalPosition);
			}
		}
		
		protected override async Task SubscribeToStreamAsync() {
			if (!ShouldStop) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to <all>: subscribing...", SubscriptionName);

				var subscription = await Connection.SubscribeToAllAsync(ResolveLinkTos, EnqueuePushedEvent,
					ServerSubscriptionDropped, UserCredentials).ConfigureAwait(false);

				Subscription = subscription;
				await ReadMissedHistoricEventsAsync().ConfigureAwait(false);
			} else {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
			}
		}

		protected override Task LiveProcessingStarted(EventStoreCatchUpSubscription eventStoreCatchUpSubscription, Position lastPosition) {
			return TaskEx.CompletedTask;
		}
	}
}
