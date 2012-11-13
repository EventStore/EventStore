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
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages
{
    public abstract class CoreProjectionManagementMessage : Message
    {
        private readonly Guid _correlationId;

        protected CoreProjectionManagementMessage(Guid correlationId)
        {
            _correlationId = correlationId;
        }

        public Guid CorrelationId
        {
            get { return _correlationId; }
        }

        public class Stopped : CoreProjectionManagementMessage
        {
            public Stopped(Guid correlationId)
                : base(correlationId)
            {
            }
        }

        public class Started : CoreProjectionManagementMessage
        {
            public Started(Guid correlationId)
                : base(correlationId)
            {
            }
        }

        public class Faulted : CoreProjectionManagementMessage
        {
            private readonly string _faultedReason;

            public Faulted(Guid correlationId, string faultedReason)
                : base(correlationId)
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
            public Start(Guid correlationId)
                : base(correlationId)
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
            public Stop(Guid correlationId)
                : base(correlationId)
            {
            }
        }

        public class Kill : CoreProjectionManagementMessage
        {
            public Kill(Guid correlationId)
                : base(correlationId)
            {
            }
        }

        public class GetState : CoreProjectionManagementMessage
        {
            private readonly IEnvelope _envelope;
            private readonly string _partition;

            public GetState(IEnvelope envelope, Guid correlationId, string partition)
                : base(correlationId)
            {
                if (envelope == null) throw new ArgumentNullException("envelope");
                if (partition == null) throw new ArgumentNullException("partition");
                _envelope = envelope;
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
        }

        public class UpdateStatistics : CoreProjectionManagementMessage
        {
            public UpdateStatistics(Guid correlationId)
                : base(correlationId)
            {
            }
        }

        public class StateReport : CoreProjectionManagementMessage
        {
            private readonly string _state;
            private readonly string _partition;

            public StateReport(Guid correlationId, string partition, string state)
                : base(correlationId)
            {
                _state = state;
                _partition = partition;
            }

            public string State
            {
                get { return _state; }
            }

            public string Partition
            {
                get { return _partition; }
            }
        }

        public class StatisticsReport : CoreProjectionManagementMessage
        {
            private readonly ProjectionStatistics _statistics;

            public StatisticsReport(Guid correlationId, ProjectionStatistics statistics)
                : base(correlationId)
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
            private readonly ProjectionSourceDefintion _sourceDefintion;

            public Prepared(Guid correlationId, ProjectionSourceDefintion sourceDefintion)
                : base(correlationId)
            {
                _sourceDefintion = sourceDefintion;
            }

            public ProjectionSourceDefintion SourceDefintion
            {
                get { return _sourceDefintion; }
            }
        }

        public class CreateAndPrepare : CoreProjectionManagementMessage
        {
            private readonly IEnvelope _envelope;
            private readonly ProjectionConfig _config;
            private readonly Func<IProjectionStateHandler> _handlerFactory;
            private readonly string _name;

            public CreateAndPrepare(
                IEnvelope envelope, Guid correlationId, string name, ProjectionConfig config,
                Func<IProjectionStateHandler> handlerFactory)
                : base(correlationId)
            {
                _envelope = envelope;
                _name = name;
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
        }

        public class Dispose : CoreProjectionManagementMessage
        {
            public Dispose(Guid correlationId)
                : base(correlationId)
            {
            }
        }
    }
}
