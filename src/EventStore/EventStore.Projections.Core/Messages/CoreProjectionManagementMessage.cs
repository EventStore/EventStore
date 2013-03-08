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
using System.Text;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public abstract class CoreProjectionManagementMessage : Message
    {
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
            public Started(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public class Faulted : CoreProjectionManagementMessage
        {
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
            public Start(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public class LoadStopped : CoreProjectionManagementMessage
        {
            public LoadStopped(Guid correlationId)
                : base(correlationId)
            {
            }
        }

        public class Stop : CoreProjectionManagementMessage
        {
            public Stop(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public class Kill : CoreProjectionManagementMessage
        {
            public Kill(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public class GetState : CoreProjectionManagementMessage
        {
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

        public class GetDebugState : CoreProjectionManagementMessage
        {
            private readonly IEnvelope _envelope;

            public GetDebugState(IEnvelope envelope, Guid correlationId)
                : base(correlationId)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                _envelope = envelope;
            }

            public IEnvelope Envelope
            {
                get { return _envelope; }
            }

        }

        public class UpdateStatistics : CoreProjectionManagementMessage
        {
            public UpdateStatistics(Guid projectionId)
                : base(projectionId)
            {
            }
        }

        public abstract class DataReportBase : CoreProjectionManagementMessage
        {
            private readonly Guid _correlationId;
            private readonly Exception _exception;
            private readonly string _partition;

            protected DataReportBase(Guid correlationId, Guid projectionId, string partition, Exception exception = null)
                : base(projectionId)
            {
                _correlationId = correlationId;
                _exception = exception;
                _partition = partition;
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
        }

        public class StateReport : DataReportBase
        {
            private readonly string _state;

            public StateReport(Guid correlationId, Guid projectionId, string partition, string state, Exception exception = null)
                : base(correlationId, projectionId, partition, exception)
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
            private readonly string _result;

            public ResultReport(Guid correlationId, Guid projectionId, string partition, string result, Exception exception = null)
                : base(correlationId, projectionId, partition, exception)
            {
                _result = result;
            }

            public string Result
            {
                get { return _result; }
            }

        }
        public class DebugState : CoreProjectionManagementMessage
        {
            private readonly Event[] _events;

            public DebugState(Guid projectionId, Event[] events)
                : base(projectionId)
            {
                _events = events;
            }

            public Event[] Events
            {
                get { return _events; }
            }

            public class Event
            {
                public static Event Create(ProjectionSubscriptionMessage.CommittedEventReceived source, string partition)
                {
                    return new Event 
                        {
                            Partition = partition,
                            BodyRaw = source.Data.Data,
                            MetadataRaw = source.Data.Metadata,
                            EventType = source.Data.EventType,
                            StreamId = source.Data.EventStreamId,
                            SequenceNumber = source.Data.EventSequenceNumber,
                            Category = source.EventCategory,
                            LogPosition = source.Data.Position.PreparePosition,
                        };
                }

                public string Category { get; set; }

                public string Partition { get; set; }

                public long LogPosition { get; set; }

                public int SequenceNumber { get; set; }

                public string StreamId { get; set; }

                public string EventType { get; set; }

                public string MetadataRaw { get; set; }

                public string BodyRaw { get; set; }
            }
        }

        public class StatisticsReport : CoreProjectionManagementMessage
        {
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

        public class CreateAndPrepare : CoreProjectionManagementMessage
        {
            private readonly IEnvelope _envelope;
            private readonly ProjectionConfig _config;
            private readonly Func<IProjectionStateHandler> _handlerFactory;
            private readonly string _name;
            private readonly int _epoch;
            private readonly int _version;

            public CreateAndPrepare(
                IEnvelope envelope, Guid projectionId, string name, int epoch, int version, ProjectionConfig config,
                Func<IProjectionStateHandler> handlerFactory)
                : base(projectionId)
            {
                _envelope = envelope;
                _name = name;
                _epoch = epoch;
                _version = version;
                _config = config;
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

            public int Epoch
            {
                get { return _epoch; }
            }

            public int Version
            {
                get { return _version; }
            }
        }

        public class CreatePrepared : CoreProjectionManagementMessage
        {
            private readonly IEnvelope _envelope;
            private readonly ProjectionConfig _config;
            private readonly ISourceDefinitionConfigurator _sourceDefinition;
            private readonly string _name;
            private readonly int _epoch;
            private readonly int _version;

            public CreatePrepared(
                IEnvelope envelope, Guid projectionId, string name, int epoch, int version, ProjectionConfig config,
                ISourceDefinitionConfigurator sourceDefinition)
                : base(projectionId)
            {
                if (name == null) throw new ArgumentNullException("name");
                if (config == null) throw new ArgumentNullException("config");
                if (sourceDefinition == null) throw new ArgumentNullException("sourceDefinition");
                _envelope = envelope;
                _name = name;
                _epoch = epoch;
                _version = version;
                _config = config;
                _sourceDefinition = sourceDefinition;
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

            public ISourceDefinitionConfigurator SourceDefinition
            {
                get { return _sourceDefinition; }
            }

            public int Epoch
            {
                get { return _epoch; }
            }

            public int Version
            {
                get { return _version; }
            }
        }

        public class Dispose : CoreProjectionManagementMessage
        {
            public Dispose(Guid projectionId)
                : base(projectionId)
            {
            }
        }
    }
}
