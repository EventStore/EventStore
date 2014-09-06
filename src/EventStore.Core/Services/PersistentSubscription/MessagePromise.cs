using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    internal struct MessagePromise
    {
        public readonly Guid MessageId;
        public readonly Guid ConnectionId;
        public readonly DateTime DueTime;

        public MessagePromise(Guid messageId, Guid connectionId, DateTime dueTime)
        {
            MessageId = messageId;
            ConnectionId = connectionId;
            DueTime = dueTime;
        }
    }
}