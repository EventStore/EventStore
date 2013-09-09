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
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class QueryProcessingStrategy : DefaultProjectionProcessingStrategy
    {
        public QueryProcessingStrategy(
            string name, ProjectionVersion projectionVersion, IProjectionStateHandler stateHandler,
            ProjectionConfig projectionConfig, IQuerySources sourceDefinition, ILogger logger)
            : base(name, projectionVersion, stateHandler, projectionConfig, sourceDefinition, logger)
        {
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
            return !_sourceDefinition.DefinesFold;
        }

        public override bool GetIsSlaveProjection()
        {
            return false;
        }

        protected override IProjectionProcessingPhase[] CreateProjectionProcessingPhases(
            IPublisher publisher, Guid projectionCorrelationId, ProjectionNamesBuilder namingBuilder,
            PartitionStateCache partitionStateCache, CoreProjection coreProjection, IODispatcher ioDispatcher,
            EventProcessingProjectionProcessingPhase firstPhase)
        {

            var coreProjectionCheckpointWriter =
                new CoreProjectionCheckpointWriter(
                    namingBuilder.MakeCheckpointStreamName(), ioDispatcher, _projectionVersion, _name);
            var checkpointManager2 = new DefaultCheckpointManager(
                publisher, projectionCorrelationId, _projectionVersion, _projectionConfig.RunAs, ioDispatcher,
                _projectionConfig, _name, new PhasePositionTagger(1), namingBuilder, GetUseCheckpoints(), false,
                _sourceDefinition.DefinesFold, coreProjectionCheckpointWriter);

            IProjectionProcessingPhase writeResultsPhase;
            if (GetProducesRunningResults()
                || !string.IsNullOrEmpty(_sourceDefinition.CatalogStream) && _sourceDefinition.ByStreams)
                writeResultsPhase = new WriteQueryEofProjectionProcessingPhase(
                    1, namingBuilder.GetResultStreamName(), coreProjection, partitionStateCache, checkpointManager2);
            else
                writeResultsPhase = new WriteQueryResultProjectionProcessingPhase(
                    1, namingBuilder.GetResultStreamName(), coreProjection, partitionStateCache, checkpointManager2,
                    checkpointManager2);

            return new[] {firstPhase, writeResultsPhase};
        }

        protected override IResultEventEmitter CreateResultEmitter(ProjectionNamesBuilder namingBuilder)
        {
            return new ResultEventEmitter(namingBuilder);
        }

    }
}
