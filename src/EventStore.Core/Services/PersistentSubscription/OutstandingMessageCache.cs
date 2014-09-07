using System;
using System.Collections.Generic;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class OutstandingMessageCache
    {
        private readonly Dictionary<Guid, OutstandingMessage> _outstandingRequests;
        private readonly PairingHeap<RetryableMessage> _byTime;
        private readonly SortedDictionary<int, Guid> _bySequences;
 
        public OutstandingMessageCache()
        {
            _outstandingRequests = new Dictionary<Guid, OutstandingMessage>();
            _byTime = new PairingHeap<RetryableMessage>((x,y) => x.DueTime < y.DueTime);
        }

        public int Count { get { return _outstandingRequests.Count; }}

        public void Remove(Guid messageId)
        {
            _outstandingRequests.Remove(messageId);
        }

        public void Remove(IEnumerable<Guid> messageIds)
        {
            foreach(var m in messageIds) Remove(m);
        }

        public void StartMessage(OutstandingMessage message, DateTime expires)
        {
            _outstandingRequests[message.EventId] = message;
            _byTime.Add(new RetryableMessage(message.EventId, expires));
        }

        public IEnumerable<RetryableMessage> GetMessagesExpiringBefore(DateTime time)
        {
            while (_byTime.Count > 0 && _byTime.FindMin().DueTime <= time)
            {
                var item = _byTime.DeleteMin();
                if (_outstandingRequests.ContainsKey(item.MessageId))
                {
                    yield return item;
                    _outstandingRequests.Remove(item.MessageId);
                }
            }
        }
    }
}