using System;
using System.Threading;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Messages
{
    public abstract partial class CoreProjectionManagementMessage_ : Message
    {
        private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

        public override int MsgTypeId
        {
            get { return TypeId; }
        }

        private readonly Guid _projectionIdId;

        protected CoreProjectionManagementMessage_(Guid projectionId)
        {
            _projectionIdId = projectionId;
        }

        public Guid ProjectionId
        {
            get { return _projectionIdId; }
        }
    }

    public class CoreProjectionManagementControlMessage : CoreProjectionManagementMessage_
    {
        private readonly Guid _workerId;
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

        public override int MsgTypeId
        {
            get { return TypeId; }
        }

        public Guid WorkerId
        {
            get { return _workerId; }
        }

        public CoreProjectionManagementControlMessage(Guid projectionId, Guid workerId)
            : base(projectionId)
        {
            _workerId = workerId;
        }
    }
}
