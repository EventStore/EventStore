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
using System.Diagnostics;
using System.Threading;
using EventStore.ClientAPI.Common.Concurrent;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI
{
    public abstract class EventStoreCatchUpSubscription
    {
        private const int DefaultReadBatchSize = 500;
        private const int DefaultMaxPushQueueSize = 10000;
        private static readonly ResolvedEvent DropSubscriptionEvent = new ResolvedEvent();

        public bool IsSubscribedToAll { get { return _streamId == string.Empty; } }
        public string StreamId { get { return _streamId; } }

        private readonly EventStoreConnection _connection;
        private readonly bool _resolveLinkTos;
        private readonly string _streamId;
        
        protected readonly int ReadBatchSize;
        protected readonly int MaxPushQueueSize;

        protected readonly Action<EventStoreCatchUpSubscription, ResolvedEvent> EventAppeared;
        private readonly Action<EventStoreCatchUpSubscription, string, Exception> _subscriptionDropped;
        
        private readonly ConcurrentQueue<ResolvedEvent> _liveQueue = new ConcurrentQueue<ResolvedEvent>();
        private EventStoreSubscription _subscription;
        private string _dropReason;
        private Exception _error;
        private volatile bool _allowProcessing;
        private int _isProcessing;

        private volatile bool _stop;
        private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);

        protected abstract void ReadEvents(EventStoreConnection connection, bool resolveLinkTos);
        protected abstract void TryProcess(ResolvedEvent e);

        protected EventStoreCatchUpSubscription(EventStoreConnection connection, 
                                                string streamId,
                                                bool resolveLinkTos,
                                                Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared, 
                                                Action<EventStoreCatchUpSubscription, string, Exception> subscriptionDropped,
                                                int readBatchSize = DefaultReadBatchSize,
                                                int maxPushQueueSize = DefaultMaxPushQueueSize)
        {
            Ensure.NotNull(connection, "connection");
            Ensure.NotNull(eventAppeared, "eventAppeared");
            Ensure.Positive(readBatchSize, "readBatchSize");
            Ensure.Positive(maxPushQueueSize, "maxPushQueueSize");

            _connection = connection;
            _streamId = string.IsNullOrEmpty(streamId) ? string.Empty : streamId;
            _resolveLinkTos = resolveLinkTos;
            ReadBatchSize = readBatchSize;
            MaxPushQueueSize = maxPushQueueSize;
            EventAppeared = eventAppeared;
            _subscriptionDropped = subscriptionDropped;
        }

        public void Start()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                _stopped.Reset();
                try
                {
                    ReadEvents(_connection, _resolveLinkTos);
                    if (!_stop)
                    {
                        var subscribeTask = _streamId == string.Empty
                            ? _connection.SubscribeToAll(_resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped)
                            : _connection.SubscribeToStream(_streamId, _resolveLinkTos, EnqueuePushedEvent, ServerSubscriptionDropped);
                        _subscription = subscribeTask.Result;
                        ReadEvents(_connection, _resolveLinkTos);
                    }
                }
                catch (Exception exc)
                {
                    DropSubscription("Error occurred during catch up/subscribing phase.", exc);
                    return;
                }

                if (_stop)
                {
                    DropSubscription("Subscription stop was requested.", null);
                    return;
                }

                _allowProcessing = true;
                EnsureProcessingPushQueue();
            });
        }

        public void Stop(TimeSpan timeout)
        {
            _stop = true;
            EnqueueSubscriptionDropNotification("Subscription stop was requested.", null);
            if (!_stopped.Wait(timeout))
                throw new TimeoutException(string.Format("Couldn't stop {0} in time.", GetType().Name));
        }

        private void EnqueuePushedEvent(EventStoreSubscription subscription, ResolvedEvent e)
        {
            if (_liveQueue.Count >= MaxPushQueueSize)
            {
                subscription.Unsubscribe();
                EnqueueSubscriptionDropNotification("Queue size limit reached.", null);
                return;
            }

            _liveQueue.Enqueue(e);
            if (_allowProcessing)
                EnsureProcessingPushQueue();
        }

        private void ServerSubscriptionDropped(EventStoreSubscription subscription)
        {
            EnqueueSubscriptionDropNotification("Subscription dropped by server.", null);
        }

        private void EnqueueSubscriptionDropNotification(string reason, Exception error)
        {
            if (Interlocked.CompareExchange(ref _dropReason, reason, null) == null)
            {
                // if reason was already set -- no need to enqueue drop again, somebody did that already
                _error = error;
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
            bool proceed = true;
            while (proceed)
            {
                ResolvedEvent e;
                while (_liveQueue.TryDequeue(out e))
                {
                    if (e.Event == null) // drop subscription artificial ResolvedEvent
                    {
                        Debug.Assert(_dropReason != null, "Drop reason not specified.");
                        DropSubscription(_dropReason, _error);
                        return;
                    }

                    try
                    {
                        TryProcess(e);
                    }
                    catch (Exception exc)
                    {
                        DropSubscription("Error occurred while processing event.", exc);
                        return;
                    }
                }
                Interlocked.CompareExchange(ref _isProcessing, 0, 1);
                // try to reacquire lock if needed
                proceed = _liveQueue.Count > 0 && Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0;
            }
        }

        private void DropSubscription(string reason, Exception error)
        {
            Ensure.NotNull(reason, "reason");
            if (_subscription != null)
                _subscription.Unsubscribe();
            if (_subscriptionDropped != null)
                _subscriptionDropped(this, reason, error);
            _stopped.Set();
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
                                                    Position? fromPositionExclusive, /* if null -- from the very beginning */
                                                    bool resolveLinkTos,
                                                    Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                    Action<EventStoreCatchUpSubscription, string, Exception> subscriptionDropped)
                : base(connection, string.Empty, resolveLinkTos, eventAppeared, subscriptionDropped)
        {
            _lastProcessedPosition = fromPositionExclusive ?? new Position(-1, -1);
            _nextReadPosition = fromPositionExclusive ?? Position.Start;
        }

        protected override void ReadEvents(EventStoreConnection connection, bool resolveLinkTos)
        {
            AllEventsSlice slice;
            do
            {
                slice = connection.ReadAllEventsForward(_nextReadPosition, ReadBatchSize, resolveLinkTos);
                foreach (var e in slice.Events)
                {
                    Debug.Assert(e.OriginalPosition.HasValue, "Subscription event came up with no OriginalPosition.");
                    TryProcess(e);
                }
                _nextReadPosition = slice.NextPosition;
            } while (!slice.IsEndOfStream);
        }

        protected override void TryProcess(ResolvedEvent e)
        {
            if (e.OriginalPosition > _lastProcessedPosition)
            {
                EventAppeared(this, e);
                _lastProcessedPosition = e.OriginalPosition.Value;
            }
        }
    }

    public class EventStoreStreamCatchUpSubscription : EventStoreCatchUpSubscription
    {
        public int LastProcessedEventNumber { get { return _lastProcessedEventNumber; } }

        private int _nextReadEventNumber;
        private int _lastProcessedEventNumber;

        internal EventStoreStreamCatchUpSubscription(EventStoreConnection connection,
                                                       string streamId,
                                                       int? fromEventNumberExclusive, /* if null -- from the very beginning */
                                                       bool resolveLinkTos,
                                                       Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                       Action<EventStoreCatchUpSubscription, string, Exception> subscriptionDropped)
            : base(connection, streamId, resolveLinkTos, eventAppeared, subscriptionDropped)
        {
            Ensure.NotNullOrEmpty(streamId, "streamId");

            _lastProcessedEventNumber = fromEventNumberExclusive ?? -1;
            _nextReadEventNumber = fromEventNumberExclusive ?? 0;
        }

        protected override void ReadEvents(EventStoreConnection connection, bool resolveLinkTos)
        {
            StreamEventsSlice slice;
            do
            {
                slice = connection.ReadStreamEventsForward(StreamId, _nextReadEventNumber, ReadBatchSize, resolveLinkTos);
                if (slice.Status != SliceReadStatus.Success)
                    return;
                foreach (var e in slice.Events)
                {
                    TryProcess(e);
                }
                _nextReadEventNumber = slice.NextEventNumber;
            } while (!slice.IsEndOfStream);
        }

        protected override void TryProcess(ResolvedEvent e)
        {
            if (e.OriginalEventNumber > _lastProcessedEventNumber)
            {
                EventAppeared(this, e);
                _lastProcessedEventNumber = e.OriginalEventNumber;
            }
        }
    }
}
