using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    namespace ParallelQueryProcessingMessages
    {
        public sealed class PartitionProcessingResult : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly Guid _correlationId;
            private readonly Guid _subscriptionId;
            private readonly string _partition;
            private readonly string _result;
            private readonly Guid _causedByGuid;
            private readonly CheckpointTag _position;

            public PartitionProcessingResult(
                Guid correlationId, Guid subscriptionId, string partition, Guid causedByGuid, CheckpointTag position,
                string result)
            {
                _correlationId = correlationId;
                _subscriptionId = subscriptionId;
                _partition = partition;
                _causedByGuid = causedByGuid;
                _position = position;
                _result = result;
            }

            public string Partition
            {
                get { return _partition; }
            }

            public string Result
            {
                get { return _result; }
            }

            public Guid CausedByGuid
            {
                get { return _causedByGuid; }
            }

            public CheckpointTag Position
            {
                get { return _position; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public Guid SubscriptionId
            {
                get { return _subscriptionId; }
            }
        }

    }
}