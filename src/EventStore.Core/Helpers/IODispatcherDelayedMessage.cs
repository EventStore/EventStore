using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Helpers
{
    public sealed class IODispatcherDelayedMessage : Message
    {
        private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }

        private readonly Guid _correlationId;
        private readonly Action _action;

        public IODispatcherDelayedMessage(Guid correlationId, Action action)
        {
            _action = action;
            _correlationId = correlationId;
        }

        public Action Action
        {
            get { return _action; }
        }

        public Guid CorrelationId
        {
            get { return _correlationId; }
        }
    }
}
