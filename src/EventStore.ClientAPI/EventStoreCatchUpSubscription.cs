using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Common.Utils.Threading;
using EventStore.ClientAPI.SystemData;
using System;
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

		/// <summary>
		/// The <see cref="IEventStoreConnection"/>.
		/// </summary>
		protected readonly IEventStoreConnection Connection;
		/// <summary>
		/// Whether to resolve LinkTo events automatically
		/// </summary>
		protected readonly bool ResolveLinkTos;
		/// <summary>
		/// The <see cref="UserCredentials"/> associated with the subscription.
		/// </summary>
		protected readonly UserCredentials UserCredentials;

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

		internal readonly ConcurrentQueueWrapper<Func<Task>> LiveQueue = new ConcurrentQueueWrapper<Func<Task>>();
		/// <summary>
		/// The underlying <see cref="EventStoreSubscription"/>.
		/// </summary>
		protected EventStoreSubscription Subscription;
		private DropData _dropData;
		/// <summary>
		/// Whether or not to process events.
		/// </summary>
		protected volatile bool AllowProcessing;
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
		protected abstract Task<Position> ReadEventsTillAsync(IEventStoreConnection connection,
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
			Connection = connection;
			Log = log;
			StreamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
			ResolveLinkTos = settings.ResolveLinkTos;
			UserCredentials = userCredentials;
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

			ShouldStop = true;
			EnqueueSubscriptionDropNotification(SubscriptionDropReason.UserInitiated, null);
		}

		private Task RunSubscriptionAsync() {
			return LoadHistoricalEventsAsync();
		}

		private async Task LoadHistoricalEventsAsync() {
			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: running...", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);

			_stopped.Reset();
			AllowProcessing = false;

			if (!ShouldStop) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to {1}: pulling events...", SubscriptionName,
						IsSubscribedToAll ? "<all>" : StreamId);

				try {
					await ReadEventsTillAsync(Connection, ResolveLinkTos, UserCredentials, null, null)
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

		/// <summary>
		/// Subscribes to the stream.
		/// </summary>
		/// <returns></returns>
		protected abstract Task SubscribeToStreamAsync();

		/// <summary>
		///
		/// </summary>
		/// <returns></returns>
		protected async Task ReadMissedHistoricEventsAsync() {
			if (!ShouldStop) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to {1}: pulling events (if left)...", SubscriptionName,
						IsSubscribedToAll ? "<all>" : StreamId);

				var lastPosition = await ReadEventsTillAsync(Connection, ResolveLinkTos, UserCredentials,
					Subscription.LastCommitPosition, Subscription.LastEventNumber).ConfigureAwait(false);
				StartLiveProcessing(lastPosition);
			} else {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
			}
		}

		private void StartLiveProcessing(Position lastPosition) {
			if (ShouldStop) {
				DropSubscription(SubscriptionDropReason.UserInitiated, null);
				return;
			}

			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: processing live events...", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);

			if (_liveProcessingStarted != null) {
				_liveProcessingStarted(this);
				LiveProcessingStarted(this, lastPosition);
			}

			if (Verbose)
				Log.Debug("Catch-up Subscription {0} to {1}: hooking to connection.Connected", SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId);

			AllowProcessing = true;
			EnsureProcessingPushQueue();
		}

		/// <summary>
		/// The callback invoked when the subscription switches to push notifications.
		/// </summary>
		/// <param name="eventStoreCatchUpSubscription"></param>
		/// <param name="lastPosition"></param>
		/// <returns></returns>
		protected abstract Task LiveProcessingStarted(EventStoreCatchUpSubscription eventStoreCatchUpSubscription,
			Position lastPosition);

		/// <summary>
		/// Enqueues a pushed event.
		/// </summary>
		/// <param name="subscription"></param>
		/// <param name="e"></param>
		/// <returns></returns>
		protected Task EnqueuePushedEvent(EventStoreSubscription subscription, ResolvedEvent e) {
			if (Verbose) {
				Log.Debug("Catch-up Subscription {0} to {1}: event appeared ({2}, {3}, {4} @ {5}).",
					SubscriptionName,
					IsSubscribedToAll ? "<all>" : StreamId,
					e.OriginalStreamId, e.OriginalEventNumber, e.OriginalEvent.EventType, e.OriginalPosition);
			}

			if (LiveQueue.Count >= MaxPushQueueSize) {
				EnqueueSubscriptionDropNotification(SubscriptionDropReason.ProcessingQueueOverflow, null);
				subscription.Unsubscribe();
				return TaskEx.CompletedTask;
			}

			EnqueueAction(() => EventAction(e));

			if (AllowProcessing)
				EnsureProcessingPushQueue();
			return TaskEx.CompletedTask;
		}

		/// <summary>
		/// Invoked when the subscription is dropped.
		/// </summary>
		/// <param name="subscription"></param>
		/// <param name="reason"></param>
		/// <param name="exc"></param>
		protected void ServerSubscriptionDropped(EventStoreSubscription subscription, SubscriptionDropReason reason,
			Exception exc) {
			EnqueueSubscriptionDropNotification(reason, exc);
		}

		/// <summary>
		/// Enqueues a dropped notification for later processing.
		/// </summary>
		/// <param name="reason"></param>
		/// <param name="error"></param>
		protected void EnqueueSubscriptionDropNotification(SubscriptionDropReason reason, Exception error) {
			// if drop data was already set -- no need to enqueue drop again, somebody did that already
			var dropData = new DropData(reason, error);
			if (Interlocked.CompareExchange(ref _dropData, dropData, null) == null) {
				LiveQueue.Enqueue(DropAction);
				if (AllowProcessing)
					EnsureProcessingPushQueue();
			}
		}

		/// <summary>
		/// Forces the subscription to process the queue if necessary.
		/// </summary>
		protected void EnsureProcessingPushQueue() {
			if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
				ThreadPool.QueueUserWorkItem(_ => ProcessLiveQueueAsync());
		}

		private async void ProcessLiveQueueAsync() {
			do {
				Func<Task> action;
				while (LiveQueue.TryDequeue(out action)) {
					await action().ConfigureAwait(false);
				}

				Interlocked.CompareExchange(ref _isProcessing, 0, 1);
			} while (!LiveQueue.IsEmpty && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
		}

		/// <summary>
		/// Places an action on the queue.
		/// </summary>
		/// <param name="action"></param>
		protected void EnqueueAction(Func<Task> action) {
			LiveQueue.Enqueue(action);
		}

		private Task DropAction() {
			_dropData = _dropData ?? new DropData(SubscriptionDropReason.Unknown,
				            new Exception("Drop reason not specified."));
			DropSubscription(_dropData.Reason, _dropData.Error);
			Interlocked.CompareExchange(ref _isProcessing, 0, 1);
			return TaskEx.CompletedTask;
		}

		private async Task EventAction(ResolvedEvent e) {
			try {
				await TryProcessAsync(e).ConfigureAwait(false);
			} catch (Exception exc) {
				Log.Debug("Catch-up Subscription {0} to {1} Exception occurred in subscription {1}",
					SubscriptionName, IsSubscribedToAll ? "<all>" : StreamId, exc);
				DropSubscription(SubscriptionDropReason.EventHandlerException, exc);
			}
		}

		internal void DropSubscription(SubscriptionDropReason reason, Exception error) {
			if (Interlocked.CompareExchange(ref _isDropped, 1, 0) == 0) {
				if (Verbose)
					Log.Debug("Catch-up Subscription {0} to {1}: dropping subscription, reason: {2} {3}.",
						SubscriptionName,
						IsSubscribedToAll ? "<all>" : StreamId,
						reason, error == null ? string.Empty : error.ToString());

				Subscription?.Unsubscribe();
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
}
