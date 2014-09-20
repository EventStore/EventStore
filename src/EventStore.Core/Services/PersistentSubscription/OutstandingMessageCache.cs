using System;
using System.Collections.Generic;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class OutstandingMessageCache
    {
        private readonly Dictionary<Guid, OutstandingMessage> _outstandingRequests;
        private readonly PairingHeap<RetryableMessage> _byTime;
        private readonly SortedList<int, int> _bySequences;
 
        public OutstandingMessageCache()
        {
            _outstandingRequests = new Dictionary<Guid, OutstandingMessage>();
            _byTime = new PairingHeap<RetryableMessage>((x,y) => x.DueTime < y.DueTime);
            _bySequences = new SortedList<int, int>();
        }

        public int Count { get { return _outstandingRequests.Count; }}

        public void Remove(Guid messageId)
        {
            OutstandingMessage m;
            if (_outstandingRequests.TryGetValue(messageId, out m))
            {
                _outstandingRequests.Remove(messageId);
                _bySequences.Remove(m.ResolvedEvent.OriginalEventNumber);
            }
        }

        public void Remove(IEnumerable<Guid> messageIds)
        {
            foreach(var m in messageIds) Remove(m);
        }

        public void StartMessage(OutstandingMessage message, DateTime expires)
        {
            bool found = _outstandingRequests.ContainsKey(message.EventId);
            _outstandingRequests[message.EventId] = message;
            if (!found)
            {
                _bySequences.Add(message.ResolvedEvent.OriginalEventNumber, message.ResolvedEvent.OriginalEventNumber);
                _byTime.Add(new RetryableMessage(message.EventId, expires));
            }
        }

        public IEnumerable<OutstandingMessage> GetMessagesExpiringBefore(DateTime time)
        {
            while (_byTime.Count > 0 && _byTime.FindMin().DueTime <= time)
            {
                var item = _byTime.DeleteMin();
                OutstandingMessage m;
                if (_outstandingRequests.TryGetValue(item.MessageId, out m))
                {
                    yield return _outstandingRequests[item.MessageId];
                    _outstandingRequests.Remove(item.MessageId);
                    _bySequences.Remove(m.ResolvedEvent.OriginalEventNumber);
                }
            }
        }

        public int GetLowestPosition()
        {
            //TODO is there a better way of doing this?
            if (_bySequences.Count == 0) return int.MinValue;
            return _bySequences.Values[0];
        }

        public bool GetMessageById(Guid id, out OutstandingMessage outstandingMessage)
        {
            return _outstandingRequests.TryGetValue(id, out outstandingMessage);
        }
    }
}