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

            private readonly string _partition;
            private readonly string _result;
            private readonly Guid _causedByGuid;
            private readonly CheckpointTag _position;

            public PartitionProcessingResult(string partition, Guid causedByGuid, CheckpointTag position, string result)
            {
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
        }

    }
}