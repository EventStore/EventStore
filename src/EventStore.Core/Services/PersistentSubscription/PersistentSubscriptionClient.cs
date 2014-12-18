using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionClient
    {
        public readonly int MaximumInFlightMessages;

        private readonly Guid _correlationId;
        private readonly Guid _connectionId;
        private readonly IEnvelope _envelope;
        private int _allowedMessages;
        public readonly string Username;
        public readonly string From;
        private readonly Stopwatch _watch;
        private long _totalItems;
        private readonly RequestStatistics _extraStatistics;
        private readonly Dictionary<Guid, ResolvedEvent> _unconfirmedEvents = new Dictionary<Guid, ResolvedEvent>();

        public int InflightMessages
        {
            get { return _unconfirmedEvents.Count; }
        }

        public PersistentSubscriptionClient(Guid correlationId,
            Guid connectionId,
            IEnvelope envelope,
            int inFlightMessages,
            string username,
            string from,
            Stopwatch watch,
            bool extraStatistics)
        {
            _correlationId = correlationId;
            _connectionId = connectionId;
            _envelope = envelope;
            _allowedMessages = inFlightMessages;
            Username = username;
            From = @from;
            _watch = watch;
            MaximumInFlightMessages = inFlightMessages;
            if (extraStatistics)
            {
                _extraStatistics = new RequestStatistics(watch, 1000);
            }
        }

        public int AvailableSlots
        {
            get { return _allowedMessages; }
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

        public void RemoveFromProcessing(Guid[] processedEventIds)
        {
            foreach (var processedEventId in processedEventIds)
            {
                if (_extraStatistics != null)
                    _extraStatistics.EndOperation(processedEventId);
                ResolvedEvent ev;
                if (!_unconfirmedEvents.TryGetValue(processedEventId, out ev)) continue;
                _unconfirmedEvents.Remove(processedEventId);
                _allowedMessages++;
            }
        }

        public void DenyProcessing(Guid[] processedEventIds)
        {
            foreach (var processedEventId in processedEventIds)
            {
                ResolvedEvent ev;
                if (_extraStatistics != null)
                    _extraStatistics.EndOperation(processedEventId);
                if (_unconfirmedEvents.TryGetValue(processedEventId, out ev))
                {
                    //it could have been timed out as well
                    _unconfirmedEvents.Remove(processedEventId);
                }
            }
        }

        public bool Push(ResolvedEvent evnt)
        {
            if (!CanSend()) { return false; }
            _allowedMessages--;
            Interlocked.Increment(ref _totalItems);
            if (_extraStatistics != null)
                _extraStatistics.StartOperation(evnt.OriginalEvent.EventId);

            _envelope.ReplyWith(new ClientMessage.PersistentSubscriptionStreamEventAppeared(CorrelationId, evnt));
            _unconfirmedEvents.Add(evnt.OriginalEvent.EventId, evnt);
            return true;
        }

        public IEnumerable<ResolvedEvent> GetUnconfirmedEvents()
        {
            return _unconfirmedEvents.Values;
        }

        public void SendDropNotification()
        {
            _envelope.ReplyWith(new ClientMessage.SubscriptionDropped(CorrelationId, SubscriptionDropReason.Unsubscribed));
        }

        public ObservedTimingMeausrement GetExtraStats()
        {
            //TODO FIX ME
            Console.WriteLine(_watch);
            return _extraStatistics == null ? null : _extraStatistics.GetMeasurementDetails();
        }

        private bool CanSend()
        {
            return AvailableSlots > 0;
        }

        public bool Remove(Guid eventId)
        {
            return _unconfirmedEvents.Remove(eventId);
        }
    }
}