using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
#if!NET452
using TaskEx = System.Threading.Tasks.Task;

#endif
namespace EventStore.ClientAPI {
	/// <summary>
	/// Base class representing catch-up subscriptions.
	/// </summary>
	public abstract class EventStoreCatchUpSubscription {
		private static readonly ResolvedEvent DropSubscriptionEvent = new ResolvedEvent();

		/// <summary>
		/// Indicates whether the subscription is to all events or to
		/// a specific stream.
		/// </summary>
		public bool IsSubscribedToAll => StreamId == string.Empty;

		/// <summary>
		/// The name of the stream to which the subscription is subscribed
		/// (empty if subscribed to all).
		/// </summary>
		public string StreamId { get; }

		/// <summary>
		/// The name of subscription.
		/// </summary>
		public string SubscriptionName { get; }

		/// <summary>
		/// The <see cref="ILogger"/> to use for the subscription.
		/// </summary>
		protected readonly ILogger Log;

		private readonly IEventStoreConnection _connection;
		private readonly bool _resolveLinkTos;
		private readonly UserCredentials _userCredentials;

		/// <summary>
		/// The batch size to use during the read phase of the subscription.
		/// </summary>
		protected readonly int ReadBatchSize;

		/// <summary>
		/// The maximum number of events to buffer before the subscription drops.
		/// </summary>
		protected readonly int MaxPushQueueSize;

		/// <summary>
		/// Action invoked when a new event appears on the subscription.
		/// </summary>
		protected readonly Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> EventAppeared;

		private readonly Action<EventStoreCatchUpSubscription> _liveProcessingStarted;
		private readonly Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> _subscriptionDropped;

		/// <summary>
		/// Whether or not to use verbose logging (useful during debugging).
		/// </summary>
		protected readonly bool Verbose;

		private readonly ConcurrentQueueWrapper<ResolvedEvent> _liveQueue = new ConcurrentQueueWrapper<ResolvedEvent>();
		private EventStoreSubscription _subscription;
		private DropData _dropData;
		private volatile bool _allowProcessing;
		private int _isProcessing;

		///<summary>
		/// stop has been called.
		///</summary>
		protected volatile bool ShouldStop;

		private int _isDropped;
		private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);

		/// <summary>
		/// Read events until the given position or event number async.
		/// </summary>
		/// <param name="connection">The connection.</param>
		/// <param name="resolveLinkTos">Whether to resolve Link events.</param>
		/// <param name="userCredentials">User credentials for the operation.</param>
		/// <param name="lastCommitPosition">The commit position to read until.</param>
		/// <param name="lastEventNumber">The event number to read until.</param>
		/// <returns></returns>
		protected abstract Task ReadEventsTillAsync(IEventStoreConnection connection,
			bool resolveLinkTos,
			UserCredentials userCredentials,
			long? lastCommitPosition,
			long? lastEventNumber);

		/// <summary>
		/// Try to process a single <see cref="ResolvedEvent"/>.
		/// </summary>
		/// <param name="e">The <see cref="ResolvedEvent"/> to process.</param>
		protected abstract Task TryProcessAsync(ResolvedEvent e);

		/// <summary>
		/// Constructs state for EventStoreCatchUpSubscription.
		/// </summary>
		/// <param name="connection">The connection.</param>
		/// <param name="log">The <see cref="ILogger"/> to use.</param>
		/// <param name="streamId">The stream name.</param>
		/// <param name="userCredentials">User credentials for the operations.</param>
		/// <param name="eventAppeared">Action invoked when events are received.</param>
		/// <param name="liveProcessingStarted">Action invoked when the read phase finishes.</param>
		/// <param name="subscriptionDropped">Action invoked if the subscription drops.</param>
		/// <param name="settings">Settings for this subscription.</param>
		protected EventStoreCatchUpSubscription(IEventStoreConnection connection,
			ILogger log,
			string streamId,
			UserCredentials userCredentials,
			Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared,
			Action<EventStoreCatchUpSubscription> liveProcessingStarted,
			Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
			CatchUpSubscriptionSettings settings) {
			Ensure.NotNull(connection, "connection");
			Ensure.NotNull(log, "log");
			Ensure.NotNull(eventAppeared, "eventAppeared");
			_connection = connection;
			Log = log;
			StreamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
			_resolveLinkTos = settings.ResolveLinkTos;
			_userCredentials = userCredentials;
			ReadBatchSize = settings.ReadBatchSize;
			MaxPushQueueSize = settings.MaxLiveQueueSize;

			EventAppeared = eventAppeared;
			_liveProcessingStarted = liveProcessingStarted;
			_subscriptionDropped = subscriptionDropped;
			Verbose = settings.VerboseLogging;
			SubscriptionName = settings.SubscriptionName ?? String.Empty;
		}

		internal Task StartAsync() {
			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: starting...", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);
			return RunSubscriptionAsync();
		}

		/// <summary>
		/// Attempts to stop the subscription blocking for completion of stop.
		/// </summary>
		/// <param name="timeout">The maximum amount of time which the current thread will block waiting for the subscription to stop before throwing a TimeoutException.</param>
		/// <exception cref="TimeoutException">Thrown if the subscription fails to stop within it's timeout period.</exception>
		public void Stop(TimeSpan timeout) {
			Stop();
			if (Verbose) Log.Debug("Waiting on subscription {0} to stop", SubscriptionName);
			if (!_stopped.Wait(timeout))
				throw new TimeoutException(string.Format("Could not stop {0} in time.", GetType().Name));
		}

		/// <summary>
		/// Attempts to stop the subscription without blocking for completion of stop
		/// </summary>
		public void Stop() {
			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: requesting stop...", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);
			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: unhooking from connection.Connected.", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);
			_connection.Connected -= OnReconnect;

			ShouldStop = true;
			EnqueueSubscriptionDropNotification(SubscriptionDropReason.UserInitiated, null);
		}

		private void OnReconnect(object sender, ClientConnectionEventArgs clientConnectionEventArgs) {
			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: recovering after reconnection.", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);
			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: unhooking from connection.Connected.", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);
			_connection.Connected -= OnReconnect;
			RunSubscriptionAsync();
		}

		private Task RunSubscriptionAsync() {
			return LoadHistoricalEventsAsync();
		}

		private async Task LoadHistoricalEventsAsync() {
			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: running...", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);

			_stopped.Reset();
			_allowProcessing = false;

			if (!ShouldStop) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to {1}: pulling events...", SubscriptionName,
						IsSubscribedToAll ? "<all>" : StreamId);

				try {
					await ReadEventsTillAsync(_connection, _resolveLinkTos, _userCredentials, null, null)
						.ConfigureAwait(false);
					await SubscribeToStreamAsync().ConfigureAwait(false);
				} catch (Exception ex) {
					DropSubscription(SubscriptionDropReason.CatchUpError, ex);
					throw;
				}
			} else {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
			}
		}

		private async Task SubscribeToStreamAsync() {
			if (!ShouldStop) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to {1}: subscribing...", SubscriptionName,
						IsSubscribedToAll ? "<all>" : StreamId);

				var subscription =
					StreamId == string.Empty
						? await _connection.SubscribeToAllAsync(_resolveLinkTos, EnqueuePushedEvent,
							ServerSubscriptionDropped, _userCredentials).ConfigureAwait(false)
						: await _connection.SubscribeToStreamAsync(StreamId, _resolveLinkTos, EnqueuePushedEvent,
							ServerSubscriptionDropped, _userCredentials).ConfigureAwait(false);

				_subscription = subscription;
				await ReadMissedHistoricEventsAsync().ConfigureAwait(false);
			} else {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
			}
		}

		private async Task ReadMissedHistoricEventsAsync() {
			if (!ShouldStop) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to {1}: pulling events (if left)...", SubscriptionName,
						IsSubscribedToAll ? "<all>" : StreamId);

				await ReadEventsTillAsync(_connection, _resolveLinkTos, _userCredentials,
					_subscription.LastCommitPosition, _subscription.LastEventNumber).ConfigureAwait(false);
				StartLiveProcessing();
			} else {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
			}
		}

		private void StartLiveProcessing() {
			if (ShouldStop) {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
				return;
			}

			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: processing live events...", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);

			if (_liveProcessingStarted != null)
				_liveProcessingStarted(this);

			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: hooking to connection.Connected", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);
			_connection.Connected += OnReconnect;

			_allowProcessing = true;
			EnsureProcessingPushQueue();
		}

		private Task EnqueuePushedEvent(EventStoreSubscription subscription, ResolvedEvent e) {
			if (Verbose) {
				Log.Debug("Catch-up Subscription {0} to {1}: event appeared ({2}, {3}, {4} @ {5}).",
					SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId,
					e.OriginalStreamId, e.OriginalEventNumber, e.OriginalEvent.EventType, e.OriginalPosition);
			}

			if (_liveQueue.Count >= MaxPushQueueSize) {
				EnqueueSubscriptionDropNotification(SubscriptionDropReason.ProcessingQueueOverflow, null);
				subscription.Unsubscribe();
				return TaskEx.CompletedTask;
			}

			_liveQueue.Enqueue(e);

			if (_allowProcessing)
				EnsureProcessingPushQueue();
			return TaskEx.CompletedTask;
		}

		private void ServerSubscriptionDropped(EventStoreSubscription subscription, SubscriptionDropReason reason,
			Exception exc) {
			EnqueueSubscriptionDropNotification(reason, exc);
		}

		private void EnqueueSubscriptionDropNotification(SubscriptionDropReason reason, Exception error) {
			// if drop data was already set -- no need to enqueue drop again, somebody did that already
			var dropData = new DropData(reason, error);
			if (Interlocked.CompareExchange(ref _dropData, dropData, null) == null) {
				_liveQueue.Enqueue(DropSubscriptionEvent);
				if (_allowProcessing)
					EnsureProcessingPushQueue();
			}
		}

		private void EnsureProcessingPushQueue() {
			if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
				ThreadPool.QueueUserWorkItem(_ => ProcessLiveQueueAsync());
		}

		private async void ProcessLiveQueueAsync() {
			do {
				ResolvedEvent e;
				while (_liveQueue.TryDequeue(out e)) {
					if (e.Equals(DropSubscriptionEvent)) // drop subscription artificial ResolvedEvent
					{
						_dropData = _dropData ?? new DropData(SubscriptionDropReason.Unknown,
							            new Exception("Drop reason not specified."));
						DropSubscription(_dropData.Reason, _dropData.Error);
						Interlocked.CompareExchange(ref _isProcessing, 0, 1);
						return;
					}

					try {
						await TryProcessAsync(e).ConfigureAwait(false);
					} catch (Exception exc) {
						Log.Debug("Catch-up Subscription {0} to {1} Exception occurred in subscription {1}",
							SubscriptionName, IsSubscribedToAll ? "<all>" : StreamId, exc);
						DropSubscription(SubscriptionDropReason.EventHandlerException, exc);
						return;
					}
				}

				Interlocked.CompareExchange(ref _isProcessing, 0, 1);
			} while (!_liveQueue.IsEmpty && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
		}

		internal void DropSubscription(SubscriptionDropReason reason, Exception error) {
			if (Interlocked.CompareExchange(ref _isDropped, 1, 0) == 0) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to {1}: dropping subscription, reason: {2} {3}.",
						SubscriptionName,
						IsSubscribedToAll ? "<all>" : StreamId,
						reason, error == null ? string.Empty : error.ToString());

				_subscription?.Unsubscribe();
				_subscriptionDropped?.Invoke(this, reason, error);
				_stopped.Set();
			}
		}

		private class DropData {
			public readonly SubscriptionDropReason Reason;
			public readonly Exception Error;

			public DropData(SubscriptionDropReason reason, Exception error) {
				Reason = reason;
				Error = error;
			}
		}
	}

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
		protected override Task ReadEventsTillAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials, long? lastCommitPosition, long? lastEventNumber) =>
			ReadEventsInternalAsync(connection, resolveLinkTos, userCredentials, lastCommitPosition);

		private async Task ReadEventsInternalAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials, long? lastCommitPosition) {
			bool shouldStopOrDone;
			do {
				var slice = await connection
					.ReadAllEventsForwardAsync(_nextReadPosition, ReadBatchSize, resolveLinkTos, userCredentials)
					.ConfigureAwait(false);
				shouldStopOrDone = await ReadEventsCallbackAsync(slice, lastCommitPosition).ConfigureAwait(false);
			} while (!shouldStopOrDone);
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
	}

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
		protected override Task ReadEventsTillAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials,
			long? lastCommitPosition, long? lastEventNumber) =>
			ReadEventsInternalAsync(connection, resolveLinkTos, userCredentials, lastEventNumber);

		private async Task ReadEventsInternalAsync(IEventStoreConnection connection, bool resolveLinkTos,
			UserCredentials userCredentials, long? lastEventNumber) {
			bool shouldStopOrDone;
			do {
				var slice = await connection
					.ReadStreamEventsForwardAsync(StreamId, _nextReadEventNumber, ReadBatchSize, resolveLinkTos,
						userCredentials).ConfigureAwait(false);
				shouldStopOrDone = await ReadEventsCallbackAsync(slice, lastEventNumber).ConfigureAwait(false);
			} while (!shouldStopOrDone);
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
	}
}
