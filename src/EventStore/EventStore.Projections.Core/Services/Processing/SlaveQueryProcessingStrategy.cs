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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class SlaveQueryProcessingStrategy : DefaultProjectionProcessingStrategy
    {
        private readonly IPublisher _resultsPublisher;
        private readonly Guid _masterCoreProjectionId;

        public SlaveQueryProcessingStrategy(
            string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
            ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger,
            IPublisher resultsPublisher, Guid masterCoreProjectionId,
            ReaderSubscriptionDispatcher subscriptionDispatcher)
            : base(
                name, projectionVersion, stateHandler, projectionConfig, sourceDefinition, logger,
                subscriptionDispatcher)
        {
            _resultsPublisher = resultsPublisher;
            _masterCoreProjectionId = masterCoreProjectionId;
        }

        public override bool GetStopOnEof()
        {
            return true;
        }

        public override bool GetUseCheckpoints()
        {
            return false;
        }

        public override bool GetProducesRunningResults()
        {
            return false;
        }

        public override bool GetIsSlaveProjection()
        {
            return true;
        }

        public override SlaveProjectionDefinitions GetSlaveProjections()
        {
            return null;
        }

        protected override IProjectionProcessingPhase[] CreateProjectionProcessingPhases(
            IPublisher publisher, Guid projectionCorrelationId, ProjectionNamesBuilder namingBuilder,
            PartitionStateCache partitionStateCache, CoreProjection coreProjection, IODispatcher ioDispatcher,
            IProjectionProcessingPhase firstPhase)
        {
            return new[] {firstPhase};
        }

        protected override IResultEventEmitter CreateFirstPhaseResultEmitter(ProjectionNamesBuilder namingBuilder)
        {
            throw new NotImplementedException();
        }

        protected override IReaderStrategy CreateReaderStrategy(ITimeProvider timeProvider)
        {
            return new ExternallyFedReaderStrategy(
                0, _projectionConfig.RunAs, timeProvider, _sourceDefinition.LimitingCommitPosition ?? long.MinValue);
        }

        protected override IResultWriter CreateFirstPhaseResultWriter(
            IEmittedEventWriter emittedEventWriter, CheckpointTag zeroCheckpointTag,
            ProjectionNamesBuilder namingBuilder)
        {
            return new SlaveResultWriter(_resultsPublisher, _masterCoreProjectionId);
        }

        protected override ICoreProjectionCheckpointManager CreateCheckpointManager(
            Guid projectionCorrelationId, IPublisher publisher, IODispatcher ioDispatcher, ProjectionNamesBuilder namingBuilder,
            CoreProjectionCheckpointWriter coreProjectionCheckpointWriter, bool definesFold, IReaderStrategy readerStrategy)
        {
            return new NoopCheckpointManager(
                publisher, projectionCorrelationId, _projectionConfig, _name, readerStrategy.PositionTagger,
                namingBuilder);
        }

        protected override StatePartitionSelector CreateStatePartitionSelector()
        {
            return new ByPositionStreamStatePartitionSelector();
        }
    }
}
