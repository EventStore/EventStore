// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;
using System.Security.Principal;
using System.Text;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public abstract class CoreProjectionManagementMessage : Message
    {
        private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }

        private readonly Guid _projectionIdId;

        protected CoreProjectionManagementMessage(Guid projectionId)
        {
            _projectionIdId = projectionId;
        }

        public Guid ProjectionId
        {
            get { return _projectionIdId; }
        }

        public class Stopped : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private bool _completed;

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

        public class Started : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Started(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public class Faulted : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

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

        public class Start : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Start(Guid projectionId)
                : base(projectionId)
            {
            }


        }

        public class LoadStopped : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public LoadStopped(Guid correlationId)
                : base(correlationId)
            {
            }
        }

        public class Stop : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Stop(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public class Kill : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Kill(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public class GetState : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly IEnvelope _envelope;
            private readonly Guid _correlationId;
            private readonly string _partition;

            public GetState(IEnvelope envelope, Guid correlationId, Guid projectionId, string partition)
                : base(projectionId)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (partition == null) throw new ArgumentNullException("partition");
                _envelope = envelope;
                _correlationId = correlationId;
                _partition = partition;
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Partition
            {
                get { return _partition; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class GetResult : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly IEnvelope _envelope;
            private readonly Guid _correlationId;
            private readonly string _partition;

            public GetResult(IEnvelope envelope, Guid correlationId, Guid projectionId, string partition)
                : base(projectionId)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (partition == null) throw new ArgumentNullException("partition");
                _envelope = envelope;
                _correlationId = correlationId;
                _partition = partition;
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public string Partition
            {
                get { return _partition; }
            }

            public Guid CorrelationId
            {
                get { return _correlationId; }
            }
        }

        public class UpdateStatistics : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public UpdateStatistics(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public abstract class DataReportBase : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly Guid _correlationId;
            private readonly Exception _exception;
            private readonly string _partition;
            private readonly CheckpointTag _position;

            protected DataReportBase(
                Guid correlationId, Guid projectionId, string partition, CheckpointTag position,
                Exception exception = null)
                : base(projectionId)
            {
                _correlationId = correlationId;
                _exception = exception;
                _partition = partition;
                _position = position;
            }

            public string Partition
            {
                get { return _partition; }
            }

            public Exception Exception
            {
                get { return _exception; }
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
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _state;

            public StateReport(
                Guid correlationId, Guid projectionId, string partition, string state, CheckpointTag position,
                Exception exception = null)
                : base(correlationId, projectionId, partition, position, exception)
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
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly string _result;

            public ResultReport(
                Guid correlationId, Guid projectionId, string partition, string result, CheckpointTag position,
                Exception exception = null)
                : base(correlationId, projectionId, partition, position, exception)
            {
                _result = result;
            }

            public string Result
            {
                get { return _result; }
            }

        }

        public class StatisticsReport : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly ProjectionStatistics _statistics;

            public StatisticsReport(Guid projectionId, ProjectionStatistics statistics)
                : base(projectionId)
            {
                _statistics = statistics;
            }

            public ProjectionStatistics Statistics
            {
                get { return _statistics; }
            }
        }

        public class Prepared : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly ProjectionSourceDefinition _sourceDefinition;
            private readonly SlaveProjectionDefinitions _slaveProjections;

            public Prepared(
                Guid projectionId, ProjectionSourceDefinition sourceDefinition,
                SlaveProjectionDefinitions slaveProjections)
                : base(projectionId)
            {
                _sourceDefinition = sourceDefinition;
                _slaveProjections = slaveProjections;
            }

            public ProjectionSourceDefinition SourceDefinition
            {
                get { return _sourceDefinition; }
            }

            public SlaveProjectionDefinitions SlaveProjections
            {
                get { return _slaveProjections; }
            }
        }

        public class CreateAndPrepare : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly IEnvelope _envelope;
            private readonly ProjectionConfig _config;
            private readonly string _handlerType;
            private readonly string _query;
            private readonly Func<IProjectionStateHandler> _handlerFactory;
            private readonly string _name;
            private readonly ProjectionVersion _version;

            public CreateAndPrepare(
                IEnvelope envelope, Guid projectionId, string name, ProjectionVersion version, ProjectionConfig config,
                string handlerType, string query, Func<IProjectionStateHandler> handlerFactory)
                : base(projectionId)
            {
                _envelope = envelope;
                _name = name;
                _version = version;
                _config = config;
                _handlerType = handlerType;
                _query = query;
                _handlerFactory = handlerFactory;
            }

            public ProjectionConfig Config
            {
                get { return _config; }
            }

            public Func<IProjectionStateHandler> HandlerFactory
            {
                get { return _handlerFactory; }
            }

            public string Name
            {
                get { return _name; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public ProjectionVersion Version
            {
                get { return _version; }
            }

            public string HandlerType
            {
                get { return _handlerType; }
            }

            public string Query
            {
                get { return _query; }
            }
        }

        public class CreateAndPrepareSlave : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

            public override int MsgTypeId
            {
                get { return TypeId; }
            }

            private readonly IEnvelope _envelope;
            private readonly IPublisher _resultsPublisher;
            private readonly Guid _masterCoreProjectionId;
            private readonly ProjectionConfig _config;
            private readonly Func<IProjectionStateHandler> _handlerFactory;
            private readonly string _name;
            private readonly ProjectionVersion _version;

            public CreateAndPrepareSlave(
                IEnvelope envelope, Guid projectionId, string name, ProjectionVersion version, ProjectionConfig config,
                IPublisher resultsPublisher, Guid masterCoreProjectionId, Func<IProjectionStateHandler> handlerFactory)
                : base(projectionId)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (name == null) throw new ArgumentNullException("name");
                if (config == null) throw new ArgumentNullException("config");
                if (resultsPublisher == null) throw new ArgumentNullException("resultsPublisher");
                if (handlerFactory == null) throw new ArgumentNullException("handlerFactory");
                _envelope = envelope;
                _name = name;
                _version = version;
                _config = config;
                _resultsPublisher = resultsPublisher;
                _masterCoreProjectionId = masterCoreProjectionId;
                _handlerFactory = handlerFactory;
            }

            public ProjectionConfig Config
            {
                get { return _config; }
            }

            public Func<IProjectionStateHandler> HandlerFactory
            {
                get { return _handlerFactory; }
            }

            public string Name
            {
                get { return _name; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public ProjectionVersion Version
            {
                get { return _version; }
            }

            public IPublisher ResultsPublisher
            {
                get { return _resultsPublisher; }
            }

            public Guid MasterCoreProjectionId
            {
                get { return _masterCoreProjectionId; }
            }

        }

        public class CreatePrepared : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            private readonly IEnvelope _envelope;
            private readonly ProjectionConfig _config;
            private readonly IQuerySources _sourceDefinition;
            private readonly string _handlerType;
            private readonly string _query;
            private readonly string _name;
            private readonly ProjectionVersion _version;

            public CreatePrepared(
                IEnvelope envelope, Guid projectionId, string name, ProjectionVersion version, ProjectionConfig config,
                IQuerySources sourceDefinition, string handlerType, string query)
                : base(projectionId)
            {
                if (name == null) throw new ArgumentNullException("name");
                if (config == null) throw new ArgumentNullException("config");
                if (sourceDefinition == null) throw new ArgumentNullException("sourceDefinition");
                if (handlerType == null) throw new ArgumentNullException("handlerType");
                if (query == null) throw new ArgumentNullException("query");
                _envelope = envelope;
                _name = name;
                _version = version;
                _config = config;
                _sourceDefinition = sourceDefinition;
                _handlerType = handlerType;
                _query = query;
            }

            public ProjectionConfig Config
            {
                get { return _config; }
            }

            public string Name
            {
                get { return _name; }
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

            public IQuerySources SourceDefinition
            {
                get { return _sourceDefinition; }
            }

            public ProjectionVersion Version
            {
                get { return _version; }
            }

            public string HandlerType
            {
                get { return _handlerType; }
            }

            public string Query
            {
                get { return _query; }
            }
        }

        public class Dispose : CoreProjectionManagementMessage
        {
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public Dispose(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public sealed class SlaveProjectionReaderAssigned : CoreProjectionManagementMessage
        {
            private readonly Guid _subscriptionId;
            private readonly Guid _readerId;
            private new static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId { get { return TypeId; } }

            public SlaveProjectionReaderAssigned(Guid projectionId, Guid subscriptionId, Guid readerId)
                : base(projectionId)
            {
                _subscriptionId = subscriptionId;
                _readerId = readerId;
            }

            public Guid SubscriptionId
            {
                get { return _subscriptionId; }
            }

            public Guid ReaderId
            {
                get { return _readerId; }
            }
        }

    }
}
