using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    namespace ParallelQueryProcessingMessages
    {
        public abstract class PartitionProcessingResultBase : Message
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            protected Guid _correlationId;
            protected Guid _subscriptionId;
            protected string _partition;

            protected PartitionProcessingResultBase(Guid correlationId, Guid subscriptionId, string partition)
            {
                _correlationId = correlationId;
                _subscriptionId = subscriptionId;
                _partition = partition;
            }

            public string Partition
            {
                get { return _partition; }
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

        public sealed class PartitionProcessingResult : PartitionProcessingResultBase
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly string _result;
            private readonly Guid _causedByGuid;
            private readonly CheckpointTag _position;

            public PartitionProcessingResult(
                Guid correlationId, Guid subscriptionId, string partition, Guid causedByGuid, CheckpointTag position,
                string result)
                : base(correlationId, subscriptionId, partition)
            {
                _correlationId = correlationId;
                _subscriptionId = subscriptionId;
                _partition = partition;
                _causedByGuid = causedByGuid;
                _position = position;
                _result = result;
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

        }

        public sealed class PartitionMeasured : PartitionProcessingResultBase
        {
            private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly int _size;

            public PartitionMeasured(Guid correlationId, Guid subscriptionId, string partition, int size)
                : base(correlationId, subscriptionId, partition)
            {
                _correlationId = correlationId;
                _subscriptionId = subscriptionId;
                _partition = partition;
                _size = size;
            }


            public int Size
            {
                get { return _size; }
            }
        }

    }
}