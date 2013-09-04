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
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public abstract class DefaultProjectionProcessingStrategy : ProjectionProcessingStrategy
    {
        private readonly IProjectionStateHandler _stateHandler;
        protected readonly ProjectionConfig _projectionConfig;
        protected readonly IQuerySources _sourceDefinition;

        protected DefaultProjectionProcessingStrategy(
            string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
            ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger)
            : base(name, projectionVersion, logger)
        {
            _stateHandler = stateHandler;
            _projectionConfig = projectionConfig;
            _sourceDefinition = sourceDefinition;
        }

        public override sealed IProjectionProcessingPhase[] CreateProcessingPhases(
            IPublisher publisher, Guid projectionCorrelationId, PartitionStateCache partitionStateCache,
            Action updateStatistics, CoreProjection coreProjection, ProjectionNamesBuilder namingBuilder,
            ITimeProvider timeProvider, IODispatcher ioDispatcher, ReaderSubscriptionDispatcher subscriptionDispatcher,
            CoreProjectionCheckpointWriter coreProjectionCheckpointWriter)
        {
            var producesResults = _sourceDefinition.ProducesResults;
            var definesFold = _sourceDefinition.DefinesFold;

            var readerStrategy = Processing.ReaderStrategy.Create(
                0, _sourceDefinition, timeProvider, _projectionConfig.StopOnEof, _projectionConfig.RunAs);
            var checkpointStrategy = CheckpointStrategy.Create(_sourceDefinition, _projectionConfig);

            var resultEmitter = CreateResultEmitter(namingBuilder);

            var zeroCheckpointTag = readerStrategy.PositionTagger.MakeZeroCheckpointTag();
            var statePartitionSelector = checkpointStrategy.CreateStatePartitionSelector(_stateHandler);

            var checkpointManager = checkpointStrategy.CreateCheckpointManager(
                projectionCorrelationId, _projectionVersion, publisher, ioDispatcher, _projectionConfig, _name,
                namingBuilder, readerStrategy.IsReadingOrderRepeatable, checkpointStrategy._runAs,
                coreProjectionCheckpointWriter, GetProducesRunningResults(), definesFold, readerStrategy);


            var firstPhase = new EventProcessingProjectionProcessingPhase(
                coreProjection, projectionCorrelationId, publisher, this, _projectionConfig, updateStatistics,
                _stateHandler, partitionStateCache, _sourceDefinition.DefinesStateTransform, GetProducesRunningResults(),
                GetDefinesFold(), _name, _logger, zeroCheckpointTag, resultEmitter, checkpointManager,
                statePartitionSelector, checkpointStrategy, timeProvider, subscriptionDispatcher, 0, readerStrategy);

            return CreateProjectionProcessingPhases(
                publisher, projectionCorrelationId, namingBuilder, partitionStateCache, updateStatistics, coreProjection,
                timeProvider, subscriptionDispatcher, checkpointStrategy, zeroCheckpointTag, resultEmitter,
                checkpointManager, statePartitionSelector, ioDispatcher, firstPhase);
        }

        protected abstract IResultEmitter CreateResultEmitter(ProjectionNamesBuilder namingBuilder);

        protected abstract IProjectionProcessingPhase[] CreateProjectionProcessingPhases(IPublisher publisher, Guid projectionCorrelationId, ProjectionNamesBuilder namingBuilder, PartitionStateCache partitionStateCache, Action updateStatistics, CoreProjection coreProjection, ITimeProvider timeProvider, ReaderSubscriptionDispatcher subscriptionDispatcher, CheckpointStrategy checkpointStrategy, CheckpointTag zeroCheckpointTag, IResultEmitter resultEmitter, ICoreProjectionCheckpointManager checkpointManager, StatePartitionSelector statePartitionSelector, IODispatcher ioDispatcher, EventProcessingProjectionProcessingPhase firstPhase);

        protected override IQuerySources GetSourceDefinition()
        {
            return _sourceDefinition;
        }

        public override bool GetIsPartitioned()
        {
            return _sourceDefinition.ByStreams || _sourceDefinition.ByCustomPartitions;
        }

        public override void EnrichStatistics(ProjectionStatistics info)
        {
            //TODO: get rid of this cast
            info.Definition = _sourceDefinition as ProjectionSourceDefinition;
        }
    }
}
