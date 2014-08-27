using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionClient
    {
        public readonly int MaximumFreeSlots;

        private readonly Guid _correlationId;
        private readonly Guid _connectionId;
        private readonly IEnvelope _envelope;
        private int _freeSlots;
        public readonly string Username;
        public readonly string From;
        private readonly Stopwatch _watch;
        private long _totalItems;
        private readonly RequestStatistics _latencyStatistics;
        private readonly List<SequencedEvent> _unconfirmedEvents = new List<SequencedEvent>();

        public PersistentSubscriptionClient(Guid correlationId, 
                                            Guid connectionId, 
                                            IEnvelope envelope, 
                                            int freeSlots, 
                                            string username, 
                                            string from,
                                            Stopwatch watch,
                                            bool trackLatency)
        {
            _correlationId = correlationId;
            _connectionId = connectionId;
            _envelope = envelope;
            _freeSlots = freeSlots;
            Username = username;
            From = @from;
            _watch = watch;
            MaximumFreeSlots = freeSlots;
            if (trackLatency)
            {
                _latencyStatistics = new RequestStatistics(watch, 1000);
            }
        }

        public int FreeSlots
        {
            get { return _freeSlots; }
        }

        public Guid ConnectionId
        {
            get { return _connectionId; }
        }

        public long TotalItems
        {
            get { return _totalItems; }
        }

        public long LastTotalItems { get; set; }

        public Guid CorrelationId
        {
            get { return _correlationId; }
        }

        public IEnumerable<SequencedEvent> ConfirmProcessing(int numberOfFreeSlots, Guid[] processedEvents)
        {
            foreach (var processedEventId in processedEvents)
            {
                var eventIndex = _unconfirmedEvents.FindIndex(x => x.Event.Event.EventId == processedEventId);
                if (eventIndex >= 0)
                {
                    _freeSlots++;
                    if(_latencyStatistics != null)
                        _latencyStatistics.EndOperation(processedEventId);
                    var evnt = _unconfirmedEvents[eventIndex];
                    _unconfirmedEvents.RemoveAt(eventIndex);
                    yield return evnt;
                }
            }
        }

        public void Push(SequencedEvent evnt)
        {
            _freeSlots--;
            Interlocked.Increment(ref _totalItems);
            _envelope.ReplyWith(new ClientMessage.PersistentSubscriptionStreamEventAppeared(CorrelationId, evnt.Event));
            if (_latencyStatistics != null)
                _latencyStatistics.StartOperation(evnt.Event.OriginalEvent.EventId);

            _unconfirmedEvents.Add(evnt);
        }

        public IEnumerable<SequencedEvent> GetUnconfirmedEvents()
        {
            return _unconfirmedEvents;
        }

        public void SendDropNotification()
        {
            _envelope.ReplyWith(new ClientMessage.SubscriptionDropped(CorrelationId, SubscriptionDropReason.Unsubscribed));
        }

        public LatencyMeausrement GetLatencyStats()
        {
            return _latencyStatistics == null ? null : _latencyStatistics.GetMeasurementDetails();
        }
    }
}