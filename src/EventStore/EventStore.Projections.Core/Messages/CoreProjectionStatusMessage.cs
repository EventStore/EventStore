using System;
using System.Threading;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public class CoreProjectionStatusMessage : CoreProjectionManagementMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

        public override int MsgTypeId
        {
            get { return TypeId; }
        }

        protected CoreProjectionStatusMessage(Guid projectionId)
            : base(projectionId)
        {
        }

        public class Started : CoreProjectionStatusMessage
        {
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public Started(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public class Faulted : CoreProjectionStatusMessage
        {
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly string _faultedReason;

            public Faulted(Guid projectionId, string faultedReason)
                : base(projectionId)
            {
                _faultedReason = faultedReason;
            }

            public string FaultedReason
            {
                get { return _faultedReason; }
            }
        }

        public abstract class DataReportBase : CoreProjectionStatusMessage
        {
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly Guid _correlationId;
            private readonly string _partition;
            private readonly CheckpointTag _position;

            protected DataReportBase(Guid correlationId, Guid projectionId, string partition, CheckpointTag position)
                : base(projectionId)
            {
                _correlationId = correlationId;
                _partition = partition;
                _position = position;
            }

            public string Partition
            {
                get { return _partition; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }

            public CheckpointTag Position
            {
                get { return _position; }
            }
        }

        public class StateReport : DataReportBase
        {
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly string _state;

            public StateReport(
                Guid correlationId,
                Guid projectionId,
                string partition,
                string state,
                CheckpointTag position)
                : base(correlationId, projectionId, partition, position)
            {
                _state = state;
            }

            public string State
            {
                get { return _state; }
            }

        }

        public class ResultReport : DataReportBase
        {
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly string _result;

            public ResultReport(
                Guid correlationId,
                Guid projectionId,
                string partition,
                string result,
                CheckpointTag position)
                : base(correlationId, projectionId, partition, position)
            {
                _result = result;
            }

            public string Result
            {
                get { return _result; }
            }

        }

        public class StatisticsReport : CoreProjectionStatusMessage
        {
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly ProjectionStatistics _statistics;
            private readonly int _sequentialNumber;

            public StatisticsReport(Guid projectionId, ProjectionStatistics statistics, int sequentialNumber)
                : base(projectionId)
            {
                _statistics = statistics;
                _sequentialNumber = sequentialNumber;
            }

            public ProjectionStatistics Statistics
            {
                get { return _statistics; }
            }

            public int SequentialNumber
            {
                get { return _sequentialNumber; }
            }
        }

        public class Prepared : CoreProjectionStatusMessage
        {
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly ProjectionSourceDefinition _sourceDefinition;

            public Prepared(Guid projectionId, ProjectionSourceDefinition sourceDefinition)
                : base(projectionId)
            {
                _sourceDefinition = sourceDefinition;
            }

            public ProjectionSourceDefinition SourceDefinition
            {
                get { return _sourceDefinition; }
            }
        }

        public class ProjectionWorkerStarted : Message
        {
            private readonly Guid _workerId;
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            public ProjectionWorkerStarted(Guid workerId)
            {
                _workerId = workerId;
            }

            public Guid WorkerId
            {
                get { return _workerId; }
            }
        }

        public class Stopped : CoreProjectionStatusMessage
        {
            private new static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly bool _completed;

            public Stopped(Guid projectionId, bool completed)
                : base(projectionId)
            {
                _completed = completed;
            }

            public bool Completed
            {
                get { return _completed; }
            }
        }
    }
}