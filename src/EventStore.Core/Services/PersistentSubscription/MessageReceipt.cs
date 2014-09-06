using System;

namespace EventStore.Core.Services.PersistentSubscription
{
    internal struct MessageReceipt
    {
        public readonly Guid MessageId;
        public readonly Guid ConnectionId;

        public MessageReceipt(Guid messageId, Guid connectionId) : this()
        {
            MessageId = messageId;
            ConnectionId = connectionId;
        }
    }
}