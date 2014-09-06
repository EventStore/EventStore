using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    internal struct MessageReceipt
    {
        public readonly Guid MessageId;
        public readonly Guid ConnectionId;
        public readonly DateTime DueTime;

        public MessageReceipt(Guid messageId, Guid connectionId, DateTime dueTime)
        {
            MessageId = messageId;
            ConnectionId = connectionId;
            DueTime = dueTime;
        }
    }
}