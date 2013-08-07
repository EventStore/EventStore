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
    public class ProjectionProcessingStrategy
    {
        private readonly string _name;
        private readonly ProjectionVersion _projectionVersion;
        private readonly IProjectionStateHandler _stateHandler;
        private readonly ProjectionConfig _projectionConfig;
        private readonly IQuerySources _sourceDefinition;
        private readonly ILogger _logger;

        public ProjectionProcessingStrategy(
            string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
            ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger)
        {
            _name = name;
            _projectionVersion = projectionVersion;
            _stateHandler = stateHandler;
            _projectionConfig = projectionConfig;
            _sourceDefinition = sourceDefinition;
            _logger = logger;
        }

        public CoreProjection Create(
            Guid projectionCorrelationId, IPublisher publisher, IODispatcher ioDispatcher,
            ReaderSubscriptionDispatcher subscriptionDispatcher, ITimeProvider timeProvider)
        {
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (timeProvider == null) throw new ArgumentNullException("timeProvider");

            var namingBuilder = new ProjectionNamesBuilder(_name, _sourceDefinition);

            return new CoreProjection(
                _projectionVersion, projectionCorrelationId, publisher, ioDispatcher, subscriptionDispatcher, _logger,
                namingBuilder, this, timeProvider, _projectionConfig.StopOnEof);
        }

        public EventProcessingProjectionProcessingPhase CreateFirstProcessingPhase(
            IPublisher publisher, Guid projectionCorrelationId, PartitionStateCache partitionStateCache,
            Action updateStatistics, CoreProjection coreProjection, ProjectionNamesBuilder namingBuilder,
            ITimeProvider timeProvider, IODispatcher ioDispatcher)
        {
            var checkpointStrategy = CheckpointStrategy.Create(0, _sourceDefinition, _projectionConfig, timeProvider);

            var resultEmitter = checkpointStrategy.CreateResultEmitter(namingBuilder);
            var zeroCheckpointTag = checkpointStrategy.ReaderStrategy.PositionTagger.MakeZeroCheckpointTag();
            var statePartitionSelector = checkpointStrategy.CreateStatePartitionSelector(_stateHandler);

            var checkpointManager = checkpointStrategy.CreateCheckpointManager(
                projectionCorrelationId, _projectionVersion, publisher, ioDispatcher, _projectionConfig, _name,
                namingBuilder, checkpointStrategy.ReaderStrategy.IsReadingOrderRepeatable, checkpointStrategy._runAs);


            var projectionProcessingPhase = new EventProcessingProjectionProcessingPhase(
                coreProjection, projectionCorrelationId, publisher, this, _projectionConfig, updateStatistics,
                _stateHandler, partitionStateCache, checkpointStrategy._definesStateTransform, _name, _logger,
                zeroCheckpointTag, resultEmitter, checkpointManager, statePartitionSelector, checkpointStrategy,
                timeProvider);
            return projectionProcessingPhase;
        }
    }
}
