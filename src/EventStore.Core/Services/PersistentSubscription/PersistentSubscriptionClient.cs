using System;
using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using System.Linq;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class PersistentSubscriptionClient
    {
        public readonly int MaximumFreeSlots;

        private readonly Guid _correlationId;
        private readonly Guid _connectionId;
        private readonly IEnvelope _envelope;
        private int _freeSlots;
        private readonly List<SequencedEvent> _unconfirmedEvents = new List<SequencedEvent>();

        public PersistentSubscriptionClient(Guid correlationId, Guid connectionId, IEnvelope envelope, int freeSlots)
        {
            _correlationId = correlationId;
            _connectionId = connectionId;
            _envelope = envelope;
            _freeSlots = freeSlots;
            MaximumFreeSlots = freeSlots;
        }

        public int FreeSlots
        {
            get { return _freeSlots; }
        }

        public Guid ConnectionId
        {
            get { return _connectionId; }
        }

        public Guid CorrelationId
        {
            get { return _correlationId; }
        }

        public IEnumerable<SequencedEvent> ConfirmProcessing(int numberOfFreeSlots, Guid[] processedEvents)
        {
            _freeSlots = numberOfFreeSlots;
            foreach (var processedEventId in processedEvents)
            {
                var eventIndex = _unconfirmedEvents.FindIndex(x => x.Event.Event.EventId == processedEventId);
                if (eventIndex >= 0)
                {
                    var evnt = _unconfirmedEvents[eventIndex];
                    _unconfirmedEvents.RemoveAt(eventIndex);
                    yield return evnt;
                }
            }
        }

        public void Push(SequencedEvent evnt)
        {
            _freeSlots--;
            _envelope.ReplyWith(new ClientMessage.PersistentSubscriptionStreamEventAppeared(CorrelationId, evnt.Event));
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
    }
}