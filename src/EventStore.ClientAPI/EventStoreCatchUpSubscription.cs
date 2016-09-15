using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Base class representing catch-up subscriptions.
    /// </summary>
    public abstract class EventStoreCatchUpSubscription
    {
        private static readonly ResolvedEvent DropSubscriptionEvent = new ResolvedEvent();

        /// <summary>
        /// Indicates whether the subscription is to all events or to
        /// a specific stream.
        /// </summary>
        public bool IsSubscribedToAll { get { return _streamId == string.Empty; } }
        /// <summary>
        /// The name of the stream to which the subscription is subscribed
        /// (empty if subscribed to all).
        /// </summary>
        public string StreamId { get { return _streamId; } }

        /// <summary>
        /// The <see cref="ILogger"/> to use for the subscription.
        /// </summary>
        protected readonly ILogger Log;

        private readonly IEventStoreConnection _connection;
        private readonly bool _resolveLinkTos;
        private readonly UserCredentials _userCredentials;
        private readonly string _streamId;

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
        protected readonly Action<EventStoreCatchUpSubscription, ResolvedEvent> EventAppeared;
        private readonly Action<EventStoreCatchUpSubscription> _liveProcessingStarted;
        private readonly Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> _subscriptionDropped;
        /// <summary>
        /// Whether or not to use verbose logging (useful during debugging).
        /// </summary>
        protected readonly bool Verbose;

        private readonly ConcurrentQueue<ResolvedEvent> _liveQueue = new ConcurrentQueue<ResolvedEvent>();
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
                                               int? lastEventNumber);
        /// <summary>
        /// Try to process a single <see cref="ResolvedEvent"/>.
        /// </summary>
        /// <param name="e">The <see cref="ResolvedEvent"/> to process.</param>
        protected abstract void TryProcess(ResolvedEvent e);

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
                                                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                Action<EventStoreCatchUpSubscription> liveProcessingStarted,
                                                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
                                                CatchUpSubscriptionSettings settings)
        {
            Ensure.NotNull(connection, "connection");
            Ensure.NotNull(log, "log");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            _connection = connection;
            Log = log;
            _streamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
            _resolveLinkTos = settings.ResolveLinkTos;
            _userCredentials = userCredentials;
            ReadBatchSize = settings.ReadBatchSize;
            MaxPushQueueSize = settings.MaxLiveQueueSize;

            EventAppeared = eventAppeared;
            _liveProcessingStarted = liveProcessingStarted;
            _subscriptionDropped = subscriptionDropped;
            Verbose = settings.VerboseLogging;
        }

        internal Task Start()
        {
            if (Verbose) Log.Debug("Catch-up Subscription to {0}: starting...", IsSubscribedToAll ? "<all>" : StreamId);
            return RunSubscription();
        }

        /// <summary>
        /// Attempts to stop the subscription.
        /// </summary>
        /// <param name="timeout">The amount of time within which the subscription should stop.</param>
        /// <exception cref="TimeoutException">Thrown if the subscription fails to stop within it's timeout period.</exception>
        public void Stop(TimeSpan timeout)
        {
            Stop();
            if (Verbose) Log.Debug("Waiting on subscription to stop");
            if (!_stopped.Wait(timeout))
                throw new TimeoutException(string.Format("Could not stop {0} in time.", GetType().Name));
        }

        /// <summary>
        /// Attempts to stop the subscription without blocking for completion of stop
        /// </summary>
        public void Stop()
        {
            if (Verbose) Log.Debug("Catch-up Subscription to {0}: requesting stop...", IsSubscribedToAll ? "<all>" : StreamId);

            if (Verbose) Log.Debug("Catch-up Subscription to {0}: unhooking from connection.Connected.");
            _connection.Connected -= OnReconnect;

            ShouldStop = true;
            EnqueueSubscriptionDropNotification(SubscriptionDropReason.UserInitiated, null);
        }

        private void OnReconnect(object sender, ClientConnectionEventArgs clientConnectionEventArgs)
        {
            if (Verbose) Log.Debug("Catch-up Subscription to {0}: recovering after reconnection.");
            if (Verbose) Log.Debug("Catch-up Subscription to {0}: unhooking from connection.Connected.");
            _connection.Connected -= OnReconnect;
            RunSubscription();
        }

        private Task RunSubscription()
        {
            return Task.Factory.StartNew(LoadHistoricalEvents, TaskCreationOptions.AttachedToParent)
                .ContinueWith(_ => HandleErrorOrContinue(_));
        }

        private void LoadHistoricalEvents()
        {
            if (Verbose) Log.Debug("Catch-up Subscription to {0}: running...", IsSubscribedToAll ? "<all>" : StreamId);

            _stopped.Reset();
            _allowProcessing = false;

            if (!ShouldStop)
            {
                if (Verbose)
                    Log.Debug("Catch-up Subscription to {0}: pulling events...", IsSubscribedToAll ? "<all>" : StreamId);

                ReadEventsTillAsync(_connection, _resolveLinkTos, _userCredentials, null, null)
                    .ContinueWith(_ => HandleErrorOrContinue(_, SubscribeToStream), TaskContinuationOptions.AttachedToParent);
            }
            else
            {
                DropSubscription(SubscriptionDropReason.UserInitiated, null);
            }
        }

        private void SubscribeToStream()
        {
            if (!ShouldStop)
            {
                if (Verbose) Log.Debug("Catch-up Subscription to {0}: subscribing...", IsSubscribedToAll ? "<all>" : StreamId);

                var subscribeTask = _streamId == string.Empty
                    ? _connection.SubscribeToAllAsync(_resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped, _userCredentials)
                    : _connection.SubscribeToStreamAsync(_streamId, _resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped, _userCredentials);

                subscribeTask.ContinueWith(_ => HandleErrorOrContinue(_, () =>
                                                                            {
                                                                                _subscription = _.Result;
                                                                                ReadMissedHistoricEvents();
                                                                            }), TaskContinuationOptions.AttachedToParent);
            }
            else
            {
                DropSubscription(SubscriptionDropReason.UserInitiated, null);
            }
        }

        private void ReadMissedHistoricEvents()
        {
            if (!ShouldStop)
            {
                if (Verbose) Log.Debug("Catch-up Subscription to {0}: pulling events (if left)...", IsSubscribedToAll ? "<all>" : StreamId);

                ReadEventsTillAsync(_connection, _resolveLinkTos, _userCredentials, _subscription.LastCommitPosition, _subscription.LastEventNumber)
                    .ContinueWith(_ => HandleErrorOrContinue(_, StartLiveProcessing), TaskContinuationOptions.AttachedToParent);
            }
            else
            {
                DropSubscription(SubscriptionDropReason.UserInitiated, null);
            }
        }

        private void StartLiveProcessing()
        {
            if (ShouldStop)
            {
                DropSubscription(SubscriptionDropReason.UserInitiated, null);
                return;
            }

            if (Verbose) Log.Debug("Catch-up Subscription to {0}: processing live events...", IsSubscribedToAll ? "<all>" : StreamId);

            if (_liveProcessingStarted != null)
                _liveProcessingStarted(this);

            if (Verbose) Log.Debug("Catch-up Subscription to {0}: hooking to connection.Connected", IsSubscribedToAll ? "<all>" : StreamId);
            _connection.Connected += OnReconnect;

            _allowProcessing = true;
            EnsureProcessingPushQueue();
        }

        private void HandleErrorOrContinue(Task task, Action continuation = null)
        {
            if (task.IsFaulted)
            {
                DropSubscription(SubscriptionDropReason.CatchUpError, task.Exception.GetBaseException());
                task.Wait();
            }
            else if (task.IsCanceled)
            {
                DropSubscription(SubscriptionDropReason.CatchUpError, new TaskCanceledException(task));
                task.Wait();
            }
            else if (continuation != null)
            {
                continuation();
            }
        }

        private void EnqueuePushedEvent(EventStoreSubscription subscription, ResolvedEvent e)
        {
            if (Verbose)
                Log.Debug("Catch-up Subscription to {0}: event appeared ({1}, {2}, {3} @ {4}).",
                          IsSubscribedToAll ? "<all>" : StreamId,
                          e.OriginalStreamId, e.OriginalEventNumber, e.OriginalEvent.EventType, e.OriginalPosition);

            if (_liveQueue.Count >= MaxPushQueueSize)
            {
                EnqueueSubscriptionDropNotification(SubscriptionDropReason.ProcessingQueueOverflow, null);
                subscription.Unsubscribe();
                return;
            }

            _liveQueue.Enqueue(e);

            if (_allowProcessing)
                EnsureProcessingPushQueue();
        }

        private void ServerSubscriptionDropped(EventStoreSubscription subscription, SubscriptionDropReason reason, Exception exc)
        {
            EnqueueSubscriptionDropNotification(reason, exc);
        }

        private void EnqueueSubscriptionDropNotification(SubscriptionDropReason reason, Exception error)
        {
            // if drop data was already set -- no need to enqueue drop again, somebody did that already
            var dropData = new DropData(reason, error);
            if (Interlocked.CompareExchange(ref _dropData, dropData, null) == null)
            {
                _liveQueue.Enqueue(DropSubscriptionEvent);
                if (_allowProcessing)
                    EnsureProcessingPushQueue();
            }
        }

        private void EnsureProcessingPushQueue()
        {
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(_ => ProcessLiveQueue());
        }

        private void ProcessLiveQueue()
        {
            do
            {
                ResolvedEvent e;
                while (_liveQueue.TryDequeue(out e))
                {
                    if (e.Equals(DropSubscriptionEvent)) // drop subscription artificial ResolvedEvent
                    {
                        if (_dropData == null) _dropData = new DropData(SubscriptionDropReason.Unknown, new Exception("Drop reason not specified."));
                        DropSubscription(_dropData.Reason, _dropData.Error);
                        Interlocked.CompareExchange(ref _isProcessing, 0, 1);
                        return;
                    }

                    try
                    {
                        TryProcess(e);
                    }
                    catch (Exception exc)
                    {
                        Log.Debug("Catch-up Subscription to {0} Exception occurred in subscription {1}",IsSubscribedToAll ? "<all>" : StreamId, exc);
                        DropSubscription(SubscriptionDropReason.EventHandlerException, exc);
                        return;
                    }
                }
                Interlocked.CompareExchange(ref _isProcessing, 0, 1);
            } while (_liveQueue.Count > 0 && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0);
        }

        private void DropSubscription(SubscriptionDropReason reason, Exception error)
        {
            if (Interlocked.CompareExchange(ref _isDropped, 1, 0) == 0)
            {
                if (Verbose)
                    Log.Debug("Catch-up Subscription to {0}: dropping subscription, reason: {1} {2}.",
                              IsSubscribedToAll ? "<all>" : StreamId, reason, error == null ? string.Empty : error.ToString());

                if (_subscription != null)
                    _subscription.Unsubscribe();
                if (_subscriptionDropped != null)
                    _subscriptionDropped(this, reason, error);
                _stopped.Set();
            }
        }

        private class DropData
        {
            public readonly SubscriptionDropReason Reason;
            public readonly Exception Error;

            public DropData(SubscriptionDropReason reason, Exception error)
            {
                Reason = reason;
                Error = error;
            }
        }
    }

    /// <summary>
    /// A catch-up subscription to all events in the Event Store.
    /// </summary>
    public class EventStoreAllCatchUpSubscription: EventStoreCatchUpSubscription
    {
        /// <summary>
        /// The last position processed on the subscription.
        /// </summary>
        public Position LastProcessedPosition
        {
            get
            {
                Position oldPos = _lastProcessedPosition;
                Position curPos;
                while (oldPos != (curPos = _lastProcessedPosition))
                {
                    oldPos = curPos;
                }
                return curPos;
            }
        }

        private Position _nextReadPosition;
        private Position _lastProcessedPosition;
        private TaskCompletionSource<bool> _completion;

        internal EventStoreAllCatchUpSubscription(IEventStoreConnection connection,
                                                  ILogger log,
                                                  Position? fromPositionExclusive, /* if null -- from the very beginning */
                                                  UserCredentials userCredentials,
                                                  Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                  Action<EventStoreCatchUpSubscription> liveProcessingStarted,
                                                  Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
                                                 CatchUpSubscriptionSettings settings)
                : base(connection, log, string.Empty, userCredentials,
                       eventAppeared, liveProcessingStarted, subscriptionDropped, settings)
        {
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
                        UserCredentials userCredentials, long? lastCommitPosition, int? lastEventNumber)
        {
            _completion = new TaskCompletionSource<bool>();
            ReadEventsInternal(connection, resolveLinkTos, userCredentials, lastCommitPosition, lastEventNumber);
            return _completion.Task;
        }

        private void ReadEventsInternal(IEventStoreConnection connection, bool resolveLinkTos,
                       UserCredentials userCredentials, long? lastCommitPosition, int? lastEventNumber)
        {
            try { 
                connection.ReadAllEventsForwardAsync(_nextReadPosition, ReadBatchSize, resolveLinkTos, userCredentials)
                .ContinueWith(_ =>
                {
                    ReadEventsCallback(_, connection, resolveLinkTos, userCredentials, lastCommitPosition, lastEventNumber);
                });
            }
            catch (Exception ex)
            {
                _completion.SetException(ex);
            }
        }

        private void ReadEventsCallback(Task<AllEventsSlice> task, IEventStoreConnection connection, bool resolveLinkTos,
                       UserCredentials userCredentials, long? lastCommitPosition, int? lastEventNumber)
        {
            try
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    _completion.SetException(task.Exception);
                    task.Wait(); //force exception to be thrown
                }

                if (!ProcessEvents(lastCommitPosition, task.Result) && !ShouldStop)
                {
                    ReadEventsInternal(connection, resolveLinkTos, userCredentials,
                        lastCommitPosition, lastEventNumber);
                }
                else
                {
                    if (Verbose)
                    {
                        Log.Debug(
                            "Catch-up Subscription to {0}: finished reading events, nextReadPosition = {1}.",
                            IsSubscribedToAll ? "<all>" : StreamId, _nextReadPosition);
                    }
                    _completion.SetResult(true);
                }
            }
            catch (Exception e)
            {
                _completion.SetException(e);
            }
        }

        private bool ProcessEvents(long? lastCommitPosition, AllEventsSlice slice)
        {
            foreach (var e in slice.Events)
            {
                if (e.OriginalPosition == null) throw new Exception("Subscription event came up with no OriginalPosition.");
                TryProcess(e);
            }
            _nextReadPosition = slice.NextPosition;

            var done = lastCommitPosition == null
                ? slice.IsEndOfStream
                : slice.NextPosition >= new Position(lastCommitPosition.Value, lastCommitPosition.Value);

            if (!done && slice.IsEndOfStream)
                Thread.Sleep(1); // we are waiting for server to flush its data
            return done;
        }

        /// <summary>
        /// Try to process a single <see cref="ResolvedEvent"/>.
        /// </summary>
        /// <param name="e">The <see cref="ResolvedEvent"/> to process.</param>
        protected override void TryProcess(ResolvedEvent e)
        {
            bool processed = false;
            if (e.OriginalPosition > _lastProcessedPosition)
            {
                EventAppeared(this, e);
                _lastProcessedPosition = e.OriginalPosition.Value;
                processed = true;
            }
            if (Verbose)
                Log.Debug("Catch-up Subscription to {0}: {1} event ({2}, {3}, {4} @ {5}).",
                          IsSubscribedToAll ? "<all>" : StreamId, processed ? "processed" : "skipping",
                          e.OriginalEvent.EventStreamId, e.OriginalEvent.EventNumber, e.OriginalEvent.EventType, e.OriginalPosition);
        }
    }

    /// <summary>
    /// A catch-up subscription to a single stream in the Event Store.
    /// </summary>
    public class EventStoreStreamCatchUpSubscription : EventStoreCatchUpSubscription
    {
        /// <summary>
        /// The last event number processed on the subscription.
        /// </summary>
        public int LastProcessedEventNumber { get { return _lastProcessedEventNumber; } }

        private int _nextReadEventNumber;
        private int _lastProcessedEventNumber;
        private TaskCompletionSource<bool> _completion;

        internal EventStoreStreamCatchUpSubscription(IEventStoreConnection connection,
                                                     ILogger log,
                                                     string streamId,
                                                     int? fromEventNumberExclusive, /* if null -- from the very beginning */
                                                     UserCredentials userCredentials,
                                                     Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                     Action<EventStoreCatchUpSubscription> liveProcessingStarted,
                                                     Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
                                                     CatchUpSubscriptionSettings settings)
            : base(connection, log, streamId, userCredentials,
                   eventAppeared, liveProcessingStarted, subscriptionDropped, settings)
        {
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
        protected override Task ReadEventsTillAsync(IEventStoreConnection connection, bool resolveLinkTos, UserCredentials userCredentials,
            long? lastCommitPosition, int? lastEventNumber)
        {
            _completion = new TaskCompletionSource<bool>();
            ReadEventsInternal(connection, resolveLinkTos, userCredentials, lastCommitPosition, lastEventNumber);
            return _completion.Task;
        }

        private void ReadEventsInternal(IEventStoreConnection connection, bool resolveLinkTos,
                       UserCredentials userCredentials, long? lastCommitPosition, int? lastEventNumber)
        {
            try {
                connection.ReadStreamEventsForwardAsync(StreamId, _nextReadEventNumber, ReadBatchSize, resolveLinkTos, userCredentials)
                .ContinueWith(_ =>
                {
                    ReadEventsCallback(_, connection, resolveLinkTos, userCredentials, lastCommitPosition, lastEventNumber);
                });
            }
            catch(Exception ex)
            {
                _completion.SetException(ex);
            }
        }

        private void ReadEventsCallback(Task<StreamEventsSlice> task, IEventStoreConnection connection, bool resolveLinkTos,
                       UserCredentials userCredentials, long? lastCommitPosition, int? lastEventNumber)
        {
            try
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    _completion.SetException(task.Exception);
                    task.Wait(); //force exception to be thrown
                }

                if (!ProcessEvents(lastEventNumber, task.Result) && !ShouldStop)
                {
                    ReadEventsInternal(connection, resolveLinkTos, userCredentials, lastCommitPosition, lastEventNumber);
                }
                else
                {
                    if (Verbose)
                    {
                        Log.Debug(
                            "Catch-up Subscription to {0}: finished reading events, nextReadEventNumber = {1}.",
                            IsSubscribedToAll ? "<all>" : StreamId, _nextReadEventNumber);
                    }
                    _completion.SetResult(true);
                }
            }
            catch (Exception e)
            {
                _completion.SetException(e);
            }
        }

        private bool ProcessEvents(int? lastEventNumber, StreamEventsSlice slice)
        {
            bool done;
            switch (slice.Status)
            {
                case SliceReadStatus.Success:
                    {
                        foreach (var e in slice.Events)
                        {
                            TryProcess(e);
                        }
                        _nextReadEventNumber = slice.NextEventNumber;
                        done = lastEventNumber == null ? slice.IsEndOfStream : slice.NextEventNumber > lastEventNumber;
                        break;
                    }
                case SliceReadStatus.StreamNotFound:
                    {
                        if (lastEventNumber.HasValue && lastEventNumber != -1)
                            throw new Exception(
                                string.Format("Impossible: stream {0} disappeared in the middle of catching up subscription.",
                                    StreamId));
                        done = true;
                        break;
                    }
                case SliceReadStatus.StreamDeleted:
                    throw new StreamDeletedException(StreamId);
                default:
                    throw new ArgumentOutOfRangeException(string.Format("Unexpected StreamEventsSlice.Status: {0}.",
                        slice.Status));
            }

            if (!done && slice.IsEndOfStream)
                Thread.Sleep(1); // we are waiting for server to flush its data
            return done;
        }

        /// <summary>
        /// Try to process a single <see cref="ResolvedEvent"/>.
        /// </summary>
        /// <param name="e">The <see cref="ResolvedEvent"/> to process.</param>
        protected override void TryProcess(ResolvedEvent e)
        {
            bool processed = false;
            if (e.OriginalEventNumber > _lastProcessedEventNumber)
            {
                EventAppeared(this, e);
                _lastProcessedEventNumber = e.OriginalEventNumber;
                processed = true;
            }
            if (Verbose)
                Log.Debug("Catch-up Subscription to {0}: {1} event ({2}, {3}, {4} @ {5}).",
                          IsSubscribedToAll ? "<all>" : StreamId, processed ? "processed" : "skipping",
                          e.OriginalEvent.EventStreamId, e.OriginalEvent.EventNumber, e.OriginalEvent.EventType, e.OriginalEventNumber);
        }
    }
}
