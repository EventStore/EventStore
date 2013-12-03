// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  

using System;
using System.Threading;
using EventStore.ClientAPI.Common.Concurrent;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI
{
    /// <summary>
    /// Base class representing catch-up subscriptions.
    /// </summary>
    public abstract class EventStoreCatchUpSubscription
    {
        private const int DefaultReadBatchSize = 500;
        private const int DefaultMaxPushQueueSize = 10000;
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

        private volatile bool _stop;
        private int _isDropped;
        private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);

        /// <summary>
        /// Read events until the given position or event number.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="resolveLinkTos">Whether to resolve Link events.</param>
        /// <param name="userCredentials">User credentials for the operation.</param>
        /// <param name="lastCommitPosition">The commit position to read until.</param>
        /// <param name="lastEventNumber">The event number to read until.</param>
        protected abstract void ReadEventsTill(IEventStoreConnection connection,
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
        /// <param name="resolveLinkTos">Whether to resolve Link events.</param>
        /// <param name="userCredentials">User credentials for the operations.</param>
        /// <param name="eventAppeared">Action invoked when events are received.</param>
        /// <param name="liveProcessingStarted">Action invoked when the read phase finishes.</param>
        /// <param name="subscriptionDropped">Action invoked if the subscription drops.</param>
        /// <param name="verboseLogging">Whether to use verbose logging.</param>
        /// <param name="readBatchSize">Batch size for use in the reading phase.</param>
        /// <param name="maxPushQueueSize">The maximum number of events to buffer before dropping the subscription.</param>
        protected EventStoreCatchUpSubscription(IEventStoreConnection connection, 
                                                ILogger log,
                                                string streamId,
                                                bool resolveLinkTos,
                                                UserCredentials userCredentials,
                                                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared, 
                                                Action<EventStoreCatchUpSubscription> liveProcessingStarted,
                                                Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
                                                bool verboseLogging,
                                                int readBatchSize = DefaultReadBatchSize,
                                                int maxPushQueueSize = DefaultMaxPushQueueSize)
        {
            Ensure.NotNull(connection, "connection");
            Ensure.NotNull(log, "log");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.Positive(readBatchSize, "readBatchSize");
            Ensure.Positive(maxPushQueueSize, "maxPushQueueSize");

            _connection = connection;
            Log = log;
            _streamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
            _resolveLinkTos = resolveLinkTos;
            _userCredentials = userCredentials;
            ReadBatchSize = readBatchSize;
            MaxPushQueueSize = maxPushQueueSize;

            EventAppeared = eventAppeared;
            _liveProcessingStarted = liveProcessingStarted;
            _subscriptionDropped = subscriptionDropped;
            Verbose = verboseLogging;
        }

        internal void Start()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (Verbose) Log.Debug("Catch-up Subscription to {0}: starting...", IsSubscribedToAll ? "<all>" : StreamId);

                _stopped.Reset();
                try
                {
                    if (Verbose) Log.Debug("Catch-up Subscription to {0}: pulling events...", IsSubscribedToAll ? "<all>" : StreamId);
                    ReadEventsTill(_connection, _resolveLinkTos, _userCredentials, null, null);
                    if (!_stop)
                    {
                        if (Verbose) Log.Debug("Catch-up Subscription to {0}: subscribing...", IsSubscribedToAll ? "<all>" : StreamId);
                        _subscription = _streamId == string.Empty
                            ? _connection.SubscribeToAll(_resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped, _userCredentials)
                            : _connection.SubscribeToStream(_streamId, _resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped, _userCredentials);

                        if (Verbose) Log.Debug("Catch-up Subscription to {0}: pulling events (if left)...", IsSubscribedToAll ? "<all>" : StreamId);
                        ReadEventsTill(_connection, _resolveLinkTos, _userCredentials, _subscription.LastCommitPosition, _subscription.LastEventNumber);
                    }
                }
                catch (Exception exc)
                {
                    DropSubscription(SubscriptionDropReason.CatchUpError, exc);
                    return;
                }

                if (_stop)
                {
                    DropSubscription(SubscriptionDropReason.UserInitiated, null);
                    return;
                }

                if (Verbose) Log.Debug("Catch-up Subscription to {0}: processing live events...", IsSubscribedToAll ? "<all>" : StreamId);
                if (_liveProcessingStarted != null) 
                    _liveProcessingStarted(this); 
                _allowProcessing = true;
                EnsureProcessingPushQueue();
            });
        }

        /// <summary>
        /// Attempts to stop the subscription.
        /// </summary>
        /// <param name="timeout">The timeout within which the subscription should stop.</param>
        /// <exception cref="TimeoutException">Thrown if the subscription fails to stop within the timeout.</exception>
        public void Stop(TimeSpan timeout)
        {
            if (Verbose) Log.Debug("Catch-up Subscription to {0}: requesting stop...", IsSubscribedToAll ? "<all>" : StreamId);

            _stop = true;
            EnqueueSubscriptionDropNotification(SubscriptionDropReason.UserInitiated, null);
            if (!_stopped.Wait(timeout))
                throw new TimeoutException(string.Format("Couldn't stop {0} in time.", GetType().Name));
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
                    if (e.Event == null) // drop subscription artificial ResolvedEvent
                    {
                        if (_dropData == null) throw new Exception("Drop reason not specified.");
                        DropSubscription(_dropData.Reason, _dropData.Error);
                        return;
                    }

                    try
                    {
                        TryProcess(e);
                    }
                    catch (Exception exc)
                    {
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

        internal EventStoreAllCatchUpSubscription(IEventStoreConnection connection,
                                                  ILogger log,
                                                  Position? fromPositionExclusive, /* if null -- from the very beginning */
                                                  bool resolveLinkTos,
                                                  UserCredentials userCredentials,
                                                  Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                  Action<EventStoreCatchUpSubscription> liveProcessingStarted,
                                                  Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
                                                  bool verboseLogging,
                                                  int readBatchSize = 500)
                : base(connection, log, string.Empty, resolveLinkTos, userCredentials, 
                       eventAppeared, liveProcessingStarted, subscriptionDropped, verboseLogging, readBatchSize)
        {
            _lastProcessedPosition = fromPositionExclusive ?? new Position(-1, -1);
            _nextReadPosition = fromPositionExclusive ?? Position.Start;
        }

        /// <summary>
        /// Read events until the given position.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="resolveLinkTos">Whether to resolve Link events.</param>
        /// <param name="userCredentials">User credentials for the operation.</param>
        /// <param name="lastCommitPosition">The commit position to read until.</param>
        /// <param name="lastEventNumber">The event number to read until.</param>
        protected override void ReadEventsTill(IEventStoreConnection connection, bool resolveLinkTos, 
                                               UserCredentials userCredentials, long? lastCommitPosition, int? lastEventNumber)
        {
            bool done;
            do
            {
                AllEventsSlice slice = connection.ReadAllEventsForward(_nextReadPosition, ReadBatchSize, resolveLinkTos, userCredentials);
                foreach (var e in slice.Events)
                {
                    if (e.OriginalPosition == null) throw new Exception("Subscription event came up with no OriginalPosition.");
                    TryProcess(e);
                }
                _nextReadPosition = slice.NextPosition;

                done = lastCommitPosition == null
                               ? slice.IsEndOfStream
                               : slice.NextPosition >= new Position(lastCommitPosition.Value, lastCommitPosition.Value);

                if (!done && slice.IsEndOfStream)
                    Thread.Sleep(1); // we are waiting for server to flush its data
            } while (!done);

            if (Verbose) 
                Log.Debug("Catch-up Subscription to {0}: finished reading events, nextReadPosition = {1}.", 
                          IsSubscribedToAll ? "<all>" : StreamId, _nextReadPosition);
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

        internal EventStoreStreamCatchUpSubscription(IEventStoreConnection connection,
                                                     ILogger log,
                                                     string streamId,
                                                     int? fromEventNumberExclusive, /* if null -- from the very beginning */
                                                     bool resolveLinkTos,
                                                     UserCredentials userCredentials,
                                                     Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                     Action<EventStoreCatchUpSubscription> liveProcessingStarted,
                                                     Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
                                                     bool verboseLogging,
                                                     int readBatchSize = 500)
            : base(connection, log, streamId, resolveLinkTos, userCredentials, 
                   eventAppeared, liveProcessingStarted, subscriptionDropped, verboseLogging, readBatchSize)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");

            _lastProcessedEventNumber = fromEventNumberExclusive ?? -1;
            _nextReadEventNumber = fromEventNumberExclusive ?? 0;
        }

        /// <summary>
        /// Read events until the given event number.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="resolveLinkTos">Whether to resolve Link events.</param>
        /// <param name="userCredentials">User credentials for the operation.</param>
        /// <param name="lastCommitPosition">The commit position to read until.</param>
        /// <param name="lastEventNumber">The event number to read until.</param>
        protected override void ReadEventsTill(IEventStoreConnection connection, bool resolveLinkTos, 
                                               UserCredentials userCredentials, long? lastCommitPosition, int? lastEventNumber)
        {
            bool done;
            do
            {
                var slice = connection.ReadStreamEventsForward(StreamId, _nextReadEventNumber, ReadBatchSize, resolveLinkTos, userCredentials);
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
                            throw new Exception(string.Format("Impossible: stream {0} disappeared in the middle of catching up subscription.", StreamId));
                        done = true;
                        break;
                    }
                    case SliceReadStatus.StreamDeleted:
                        throw new StreamDeletedException(StreamId);
                    default:
                        throw new ArgumentOutOfRangeException(string.Format("Unexpected StreamEventsSlice.Status: {0}.", slice.Status));
                }

                if (!done && slice.IsEndOfStream)
                    Thread.Sleep(1); // we are waiting for server to flush its data
            } while (!done);

            if (Verbose)
                Log.Debug("Catch-up Subscription to {0}: finished reading events, nextReadEventNumber = {1}.",
                          IsSubscribedToAll ? "<all>" : StreamId, _nextReadEventNumber);
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
