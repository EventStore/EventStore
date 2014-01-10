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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class ProjectionProcessingStrategy 
    {
        protected readonly string _name;
        protected readonly ProjectionVersion _projectionVersion;
        protected readonly ILogger _logger;

        protected ProjectionProcessingStrategy(string name, ProjectionVersion projectionVersion, ILogger logger)
        {
            _name = name;
            _projectionVersion = projectionVersion;
            _logger = logger;
        }

        public CoreProjection Create(
            Guid projectionCorrelationId, IPublisher inputQueue, IPrincipal runAs, IPublisher publisher,
            IODispatcher ioDispatcher, ReaderSubscriptionDispatcher subscriptionDispatcher, ITimeProvider timeProvider)
        {
            if (inputQueue == null) throw new ArgumentNullException("inputQueue");
            //if (runAs == null) throw new ArgumentNullException("runAs");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");

            var namingBuilder = new ProjectionNamesBuilder(_name, GetSourceDefinition());

            var coreProjectionCheckpointWriter =
                new CoreProjectionCheckpointWriter(
                    namingBuilder.MakeCheckpointStreamName(), ioDispatcher, _projectionVersion,
                    namingBuilder.EffectiveProjectionName);

            var partitionStateCache = new PartitionStateCache();

            return new CoreProjection(
                this, _projectionVersion, projectionCorrelationId, inputQueue, runAs, publisher, ioDispatcher,
                subscriptionDispatcher, _logger, namingBuilder, coreProjectionCheckpointWriter, partitionStateCache,
                namingBuilder.EffectiveProjectionName, timeProvider, GetIsSlaveProjection());
        }

        protected abstract IQuerySources GetSourceDefinition();

        public abstract bool GetStopOnEof();
        public abstract bool GetUseCheckpoints();
        public abstract bool GetRequiresRootPartition();
        public abstract bool GetProducesRunningResults();
        public abstract bool GetIsSlaveProjection();
        public abstract void EnrichStatistics(ProjectionStatistics info);

        public abstract IProjectionProcessingPhase[] CreateProcessingPhases(
            IPublisher publisher, Guid projectionCorrelationId, PartitionStateCache partitionStateCache,
            Action updateStatistics, CoreProjection coreProjection, ProjectionNamesBuilder namingBuilder,
            ITimeProvider timeProvider, IODispatcher ioDispatcher,
            CoreProjectionCheckpointWriter coreProjectionCheckpointWriter);

        public abstract SlaveProjectionDefinitions GetSlaveProjections();
    }
}
