using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;

namespace EventStore.Core.Services.PersistentSubscription
{
    internal class PersistentSubscriptionClientCollection
    {
        //TODO this is likely faster with a list etc as the counts are very small
        private readonly Dictionary<Guid, PersistentSubscriptionClient> _hash = new Dictionary<Guid, PersistentSubscriptionClient>();
        private readonly Queue<PersistentSubscriptionClient> _queue = new Queue<PersistentSubscriptionClient>();
        private bool _preferRoundRobin;


        public int Count { get { return _hash.Count; } }

        public PersistentSubscriptionClientCollection(bool preferRoundRobin)
        {
            _preferRoundRobin = preferRoundRobin;
        }

        public void AddClient(PersistentSubscriptionClient client)
        {
            _hash.Add(client.CorrelationId, client);
            _queue.Enqueue(client);
        }

        public bool PushMessageToClient(ResolvedEvent ev)
        {
            for (var i = 0; i < _queue.Count; i++)
            {
                var current = _queue.Peek();
                try
                {
                    if (current.Push(ev))
                    {
                        return true;
                    }
                }
                finally
                {
                    if (_preferRoundRobin) 
                        _queue.Enqueue(_queue.Dequeue());
                }
            }
            return false;
        }

        public IEnumerable<ResolvedEvent> RemoveClientByConnectionId(Guid connectionId)
        {
            var clients = _hash.Values.Where(x => x.ConnectionId == connectionId).ToList();
            return clients.SelectMany(client => RemoveClientByCorrelationId(client.CorrelationId, false));
        }

        public void ShutdownAll()
        {
            while (_queue.Count > 0)
            {
                var client = _queue.Dequeue();
                RemoveClientByCorrelationId(client.CorrelationId, true);
            }
        }

        public IEnumerable<ResolvedEvent> RemoveClientByCorrelationId(Guid correlationId, bool sendDropNotification)
        {
            PersistentSubscriptionClient client;
            if (!_hash.TryGetValue(correlationId, out client)) return new ResolvedEvent[0];
            _hash.Remove(client.CorrelationId);
            for(var i=0;i<_queue.Count;i++)
            {
                var current = _queue.Dequeue();
                if (current == client) break;
                _queue.Enqueue(current);
            }
            if (sendDropNotification)
            {
                client.SendDropNotification();
            }
            return client.GetUnconfirmedEvents();
        }

        public IEnumerable<PersistentSubscriptionClient> GetAll()
        {
            return _hash.Values;
        }

        //TODO CC Maybe its better to call directly?
        public void AcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds)
        {
            PersistentSubscriptionClient client;
            if (!_hash.TryGetValue(correlationId, out client)) return;
            client.ConfirmProcessing(processedEventIds);
        }

        public void NotAcknowledgeMessagesProcessed(Guid correlationId, Guid[] processedEventIds)
        {
            PersistentSubscriptionClient client;
            if (!_hash.TryGetValue(correlationId, out client)) return;
            client.DenyProcessing(processedEventIds);
        }
    }
}