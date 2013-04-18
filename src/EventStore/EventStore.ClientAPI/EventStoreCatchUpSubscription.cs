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

namespace EventStore.ClientAPI
{
    public abstract class EventStoreCatchUpSubscription
    {
        private const int DefaultReadBatchSize = 500;
        private const int DefaultMaxPushQueueSize = 10000;
        private static readonly ResolvedEvent DropSubscriptionEvent = new ResolvedEvent();

        public bool IsSubscribedToAll { get { return _streamId == string.Empty; } }
        public string StreamId { get { return _streamId; } }

        protected readonly ILogger Log;

        private readonly EventStoreConnection _connection;
        private readonly bool _resolveLinkTos;
        private readonly string _streamId;
        
        protected readonly int ReadBatchSize;
        protected readonly int MaxPushQueueSize;

        protected readonly Action<EventStoreCatchUpSubscription, ResolvedEvent> EventAppeared;
        private readonly Action<EventStoreCatchUpSubscription> _liveProcessingStarted;
        private readonly Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> _subscriptionDropped;
        protected readonly bool Verbose;

        private readonly ConcurrentQueue<ResolvedEvent> _liveQueue = new ConcurrentQueue<ResolvedEvent>();
        private EventStoreSubscription _subscription;
        private DropData _dropData;
        private volatile bool _allowProcessing;
        private int _isProcessing;

        private volatile bool _stop;
        private int _isDropped;
        private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);

        protected abstract void ReadEventsTill(EventStoreConnection connection, bool resolveLinkTos, long? lastCommitPosition, int? lastEventNumber);
        protected abstract void TryProcess(ResolvedEvent e);

        protected EventStoreCatchUpSubscription(EventStoreConnection connection, 
                                                ILogger log,
                                                string streamId,
                                                bool resolveLinkTos,
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
            ReadBatchSize = readBatchSize;
            MaxPushQueueSize = maxPushQueueSize;

            EventAppeared = eventAppeared;
            _liveProcessingStarted = liveProcessingStarted;
            _subscriptionDropped = subscriptionDropped;
            Verbose = verboseLogging;
        }

        public void Start()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (Verbose) Log.Debug("Catch-up Subscription to {0}: starting...", IsSubscribedToAll ? "<all>" : StreamId);

                _stopped.Reset();
                try
                {
                    if (Verbose) Log.Debug("Catch-up Subscription to {0}: pulling events...", IsSubscribedToAll ? "<all>" : StreamId);
                    ReadEventsTill(_connection, _resolveLinkTos, null, null);
                    if (!_stop)
                    {
                        if (Verbose) Log.Debug("Catch-up Subscription to {0}: subscribing...", IsSubscribedToAll ? "<all>" : StreamId);
                        var subscribeTask = _streamId == string.Empty
                            ? _connection.SubscribeToAll(_resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped)
                            : _connection.SubscribeToStream(_streamId, _resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped);
                        _subscription = subscribeTask.Result;

                        if (Verbose) Log.Debug("Catch-up Subscription to {0}: pulling events (if left)...", IsSubscribedToAll ? "<all>" : StreamId);
                        ReadEventsTill(_connection, _resolveLinkTos, _subscription.LastCommitPosition, _subscription.LastEventNumber);
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

    public class EventStoreAllCatchUpSubscription: EventStoreCatchUpSubscription
    {
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

        internal EventStoreAllCatchUpSubscription(EventStoreConnection connection,
                                                  ILogger log,
                                                  Position? fromPositionExclusive, /* if null -- from the very beginning */
                                                  bool resolveLinkTos,
                                                  Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                  Action<EventStoreCatchUpSubscription> liveProcessingStarted,
                                                  Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
                                                  bool verboseLogging)
                : base(connection, log, string.Empty, resolveLinkTos, eventAppeared, liveProcessingStarted, subscriptionDropped, verboseLogging)
        {
            _lastProcessedPosition = fromPositionExclusive ?? new Position(-1, -1);
            _nextReadPosition = fromPositionExclusive ?? Position.Start;
        }

        protected override void ReadEventsTill(EventStoreConnection connection, bool resolveLinkTos, long? lastCommitPosition, int? lastEventNumber)
        {
            bool done;
            do
            {
                AllEventsSlice slice = connection.ReadAllEventsForward(_nextReadPosition, ReadBatchSize, resolveLinkTos);
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

    public class EventStoreStreamCatchUpSubscription : EventStoreCatchUpSubscription
    {
        public int LastProcessedEventNumber { get { return _lastProcessedEventNumber; } }

        private int _nextReadEventNumber;
        private int _lastProcessedEventNumber;

        internal EventStoreStreamCatchUpSubscription(EventStoreConnection connection,
                                                     ILogger log,
                                                     string streamId,
                                                     int? fromEventNumberExclusive, /* if null -- from the very beginning */
                                                     bool resolveLinkTos,
                                                     Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                     Action<EventStoreCatchUpSubscription> liveProcessingStarted,
                                                     Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped,
                                                     bool verboseLogging)
            : base(connection, log, streamId, resolveLinkTos, eventAppeared, liveProcessingStarted, subscriptionDropped, verboseLogging)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");

            _lastProcessedEventNumber = fromEventNumberExclusive ?? -1;
            _nextReadEventNumber = fromEventNumberExclusive ?? 0;
        }

        protected override void ReadEventsTill(EventStoreConnection connection, bool resolveLinkTos, long? lastCommitPosition, int? lastEventNumber)
        {
            bool done;
            do
            {
                var slice = connection.ReadStreamEventsForward(StreamId, _nextReadEventNumber, ReadBatchSize, resolveLinkTos);
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
