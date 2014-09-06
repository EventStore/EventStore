using System;
using System.Collections.Generic;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.PersistentSubscription
{
    public class OutstandingMessageCache
    {
        private readonly Dictionary<Guid, OutstandingMessage> _outstandingRequests;
        private readonly PairingHeap<RetryableMessage> _promises;

        public OutstandingMessageCache()
        {
            _outstandingRequests = new Dictionary<Guid, OutstandingMessage>();
            _promises = new PairingHeap<RetryableMessage>((x,y) => x.DueTime < y.DueTime);
        }

        public int Count { get { return _outstandingRequests.Count; }}

        public void MarkCompleted(Guid messageId)
        {
            _outstandingRequests.Remove(messageId);
        }

        public void StartMessage(OutstandingMessage message, DateTime expires)
        {
            _outstandingRequests[message.EventId] = message;
            _promises.Add(new RetryableMessage(message.EventId, expires));
        }

        public IEnumerable<RetryableMessage> GetMessagesExpiringBefore(DateTime time)
        {
            while (_promises.Count > 0 && _promises.FindMin().DueTime <= time)
            {
                var item = _promises.DeleteMin();
                if (_outstandingRequests.ContainsKey(item.MessageId))
                {
                    yield return item;
                    _outstandingRequests.Remove(item.MessageId);
                }
            }
        }
    }
}