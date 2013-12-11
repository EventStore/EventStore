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
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ParallelQueryProcessingStrategy : EventReaderBasedProjectionProcessingStrategy
    {
        private readonly IProjectionStateHandler _stateHandler;
        private new readonly ProjectionConfig _projectionConfig;
        private new readonly IQueryDefinition _sourceDefinition;
        private readonly ProjectionNamesBuilder _namesBuilder;

        private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;

        public ParallelQueryProcessingStrategy(
            string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
            Func<IProjectionStateHandler> handlerFactory, ProjectionConfig projectionConfig,
            IQueryDefinition sourceDefinition, ProjectionNamesBuilder namesBuilder, ILogger logger,
            SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher,
            ReaderSubscriptionDispatcher subscriptionDispatcher)
            : base(name, projectionVersion, projectionConfig, sourceDefinition, logger, subscriptionDispatcher)
        {
            _stateHandler = stateHandler;
            _projectionConfig = projectionConfig;
            _sourceDefinition = sourceDefinition;
            _namesBuilder = namesBuilder;
            _spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
        }

        protected override IResultEventEmitter CreateFirstPhaseResultEmitter(ProjectionNamesBuilder namingBuilder)
        {
            return new ResultEventEmitter(namingBuilder);
        }

        protected override IProjectionProcessingPhase[] CreateProjectionProcessingPhases(
            IPublisher publisher, Guid projectionCorrelationId, ProjectionNamesBuilder namingBuilder,
            PartitionStateCache partitionStateCache, CoreProjection coreProjection, IODispatcher ioDispatcher,
            IProjectionProcessingPhase firstPhase)
        {
            return new [] {firstPhase};
        }

        protected override IReaderStrategy CreateReaderStrategy(ITimeProvider timeProvider)
        {
            if (_sourceDefinition.HasCategories())
            {
                return new ParallelQueryMasterReaderStrategy(
                    0, SystemAccount.Principal, timeProvider,
                    _namesBuilder.GetCategoryCatalogStreamName(_sourceDefinition.Categories[0]));
            }
            return new ParallelQueryMasterReaderStrategy(
                0, SystemAccount.Principal, timeProvider, _sourceDefinition.CatalogStream);
        }

        protected override IProjectionProcessingPhase CreateFirstProcessingPhase(
            IPublisher publisher, Guid projectionCorrelationId, PartitionStateCache partitionStateCache,
            Action updateStatistics, CoreProjection coreProjection, ReaderSubscriptionDispatcher subscriptionDispatcher,
            CheckpointTag zeroCheckpointTag, ICoreProjectionCheckpointManager checkpointManager,
            IReaderStrategy readerStrategy, IResultWriter resultWriter)
        {
            return new ParallelQueryMasterProjectionProcessingPhase(
                coreProjection, projectionCorrelationId, publisher, _projectionConfig, updateStatistics, _stateHandler,
                partitionStateCache, _name, _logger, zeroCheckpointTag, checkpointManager, subscriptionDispatcher,
                readerStrategy, resultWriter, _projectionConfig.CheckpointsEnabled, this.GetStopOnEof(),
                _spoolProcessingResponseDispatcher);
        }

        public override bool GetStopOnEof()
        {
            return true;
        }

        public override bool GetUseCheckpoints()
        {
            return _projectionConfig.CheckpointsEnabled;
        }

        public override bool GetRequiresRootPartition()
        {
            return false;
        }

        public override bool GetProducesRunningResults()
        {
            return false;
        }

        public override bool GetIsSlaveProjection()
        {
            return false;
        }

        public override void EnrichStatistics(ProjectionStatistics info)
        {
        }

        public override SlaveProjectionDefinitions GetSlaveProjections()
        {
            return
                new SlaveProjectionDefinitions(
                    new SlaveProjectionDefinitions.Definition(
                        "slave", _sourceDefinition.HandlerType, _sourceDefinition.Query,
                        SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerThread, ProjectionMode.Transient,
                        _projectionConfig.EmitEventEnabled, _projectionConfig.CheckpointsEnabled,
                        runAs: new ProjectionManagementMessage.RunAs(_projectionConfig.RunAs), enableRunAs: true));
        }
    }

}
