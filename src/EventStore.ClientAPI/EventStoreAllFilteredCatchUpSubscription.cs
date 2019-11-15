using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using TaskEx = System.Threading.Tasks.Task;

namespace EventStore.ClientAPI {
	public class EventStoreAllFilteredCatchUpSubscription : EventStoreCatchUpSubscription {
		private Position _lastProcessedPosition;
		private Position _nextReadPosition;
		private Filter _filter;
		private int _maxSearchWindow;
		private int _checkpointIntervalMultiplier;
		private int _checkpointIntervalCurrent;
		private Func<EventStoreCatchUpSubscription, Position, Task> _checkpointReached;

		private const int DontReportCheckpointReached = -1;

		internal EventStoreAllFilteredCatchUpSubscription(IEventStoreConnection connection,
			ILogger log, Position? position, Filter filter, UserCredentials userCredentials,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Func<EventStoreCatchUpSubscription, Position, Task> checkpointReached, int checkpointIntervalMultiplier,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
			CatchUpSubscriptionFilteredSettings settings)
			: base(connection, log, string.Empty, userCredentials,
				eventAppeared, liveProcessingStarted, subscriptionDropped, settings) {
			_lastProcessedPosition = position ?? new Position(-1, -1);
			_nextReadPosition = position ?? Position.Start;
			_filter = filter;
			_maxSearchWindow = settings.MaxSearchWindow;
			_checkpointIntervalMultiplier = checkpointIntervalMultiplier;
			_checkpointReached = checkpointReached;
		}

		protected override Task<Position> ReadEventsTillAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials, long? lastCommitPosition, long? lastEventNumber) =>
			ReadEventsInternalAsync(connection, resolveLinkTos, userCredentials, lastCommitPosition);

		private async Task<Position> ReadEventsInternalAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials, long? lastCommitPosition) {
			_checkpointIntervalCurrent = 0;

			bool shouldStopOrDone;
			AllEventsSlice slice;
			do {
				slice = await connection
					.FilteredReadAllEventsForwardAsync(_nextReadPosition, ReadBatchSize, resolveLinkTos, _filter,
						_maxSearchWindow, userCredentials)
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

			await ProcessClientSideCheckpointReached(slice.NextPosition);
			_nextReadPosition = slice.NextPosition;

			var done = lastCommitPosition == null
				? slice.IsEndOfStream
				: slice.NextPosition >= new Position(lastCommitPosition.Value, lastCommitPosition.Value);

			if (!done && slice.IsEndOfStream)
				await Task.Delay(1).ConfigureAwait(false); // we are awaiting the server to flush its data
			return done;
		}

		private async Task ProcessClientSideCheckpointReached(Position position) {
			if (_checkpointIntervalMultiplier == DontReportCheckpointReached)
				return;

			_checkpointIntervalCurrent++;

			if (_checkpointIntervalCurrent >= _checkpointIntervalMultiplier) {
				await TryProcessCheckpointReachedAsync(position);
				_checkpointIntervalCurrent = 0;
			}
		}

		private async Task TryProcessCheckpointReachedAsync(Position p) {
			try {
				await _checkpointReached(this, p).ConfigureAwait(false);
			} catch (Exception ex) {
				DropSubscription(SubscriptionDropReason.EventHandlerException, ex);
				throw;
			}
		}

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

		private async Task CheckpointReachedAction(Position p) {
			try {
				await TryProcessCheckpointReachedAsync(p).ConfigureAwait(false);
			} catch (Exception exc) {
				Log.Debug("Catch-up Subscription {0} to {1} Exception occurred in subscription {1}",
					SubscriptionName, IsSubscribedToAll ? "<all>" : StreamId, exc);
				DropSubscription(SubscriptionDropReason.EventHandlerException, exc);
			}
		}

		private Task EnqueueCheckpointReached(EventStoreSubscription subscription, Position position) {
			if (Verbose) {
				Log.Debug("Catch-up Subscription {0} to <all>: checkpoint reached @ {1}.", position);
			}

			if (LiveQueue.Count >= MaxPushQueueSize) {
				EnqueueSubscriptionDropNotification(SubscriptionDropReason.ProcessingQueueOverflow, null);
				subscription.Unsubscribe();
				return TaskEx.CompletedTask;
			}

			EnqueueAction(() => CheckpointReachedAction(position));

			if (AllowProcessing)
				EnsureProcessingPushQueue();
			return TaskEx.CompletedTask;
		}

		protected override async Task SubscribeToStreamAsync() {
			if (!ShouldStop) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to <all>: subscribing...", SubscriptionName);

				var checkpointInterval = _checkpointIntervalMultiplier == DontReportCheckpointReached
					? DontReportCheckpointReached
					: _maxSearchWindow * _checkpointIntervalMultiplier;

				var subscription = await Connection.FilteredSubscribeToAllAsync(ResolveLinkTos, _filter,
					EnqueuePushedEvent, EnqueueCheckpointReached, checkpointInterval,
					ServerSubscriptionDropped,
					UserCredentials).ConfigureAwait(false);

				Subscription = subscription;
				await ReadMissedHistoricEventsAsync().ConfigureAwait(false);
			} else {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
			}
		}

		protected override async Task LiveProcessingStarted(EventStoreCatchUpSubscription eventStoreCatchUpSubscription,
			Position lastPosition) {
			await CheckpointReachedAction(lastPosition).ConfigureAwait(false);
		}
	}
}
