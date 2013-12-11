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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProcessingStrategySelector
    {
        private readonly ILogger _logger = LogManager.GetLoggerFor<ProcessingStrategySelector>();
        private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;
        private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;

        public ProcessingStrategySelector(
            ReaderSubscriptionDispatcher subscriptionDispatcher,
            SpooledStreamReadingDispatcher spoolProcessingResponseDispatcher)
        {
            _subscriptionDispatcher = subscriptionDispatcher;
            _spoolProcessingResponseDispatcher = spoolProcessingResponseDispatcher;
        }

        public ProjectionProcessingStrategy CreateProjectionProcessingStrategy(
            string name, ProjectionVersion projectionVersion, ProjectionNamesBuilder namesBuilder,
            IQueryDefinition sourceDefinition, ProjectionConfig projectionConfig,
            Func<IProjectionStateHandler> handlerFactory, IProjectionStateHandler stateHandler)
        {

            if (projectionConfig.StopOnEof && sourceDefinition.ByStreams && sourceDefinition.DefinesFold
                && !string.IsNullOrEmpty(sourceDefinition.CatalogStream))
            {
                return new ParallelQueryProcessingStrategy(
                    name, projectionVersion, stateHandler, handlerFactory, projectionConfig, sourceDefinition,
                    namesBuilder, _logger, _spoolProcessingResponseDispatcher, _subscriptionDispatcher);
            }

            if (projectionConfig.StopOnEof && sourceDefinition.ByStreams && sourceDefinition.DefinesFold
                && sourceDefinition.HasCategories())
            {
                return new ParallelQueryProcessingStrategy(
                    name, projectionVersion, stateHandler, handlerFactory, projectionConfig, sourceDefinition,
                    namesBuilder, _logger, _spoolProcessingResponseDispatcher, _subscriptionDispatcher);
            }

            return projectionConfig.StopOnEof
                ? (ProjectionProcessingStrategy)
                    new QueryProcessingStrategy(
                        name, projectionVersion, stateHandler, projectionConfig, sourceDefinition, _logger,
                        _subscriptionDispatcher)
                : new ContinuousProjectionProcessingStrategy(
                    name, projectionVersion, stateHandler, projectionConfig, sourceDefinition, _logger,
                    _subscriptionDispatcher);
        }

        public ProjectionProcessingStrategy CreateSlaveProjectionProcessingStrategy(
            string name, ProjectionVersion projectionVersion, ProjectionSourceDefinition sourceDefinition,
            ProjectionConfig projectionConfig, IProjectionStateHandler stateHandler, IPublisher resultsEnvelope,
            Guid masterCoreProjectionId, ProjectionCoreService projectionCoreService)
        {
            return new SlaveQueryProcessingStrategy(
                name, projectionVersion, stateHandler, projectionConfig, sourceDefinition, projectionCoreService.Logger,
                resultsEnvelope, masterCoreProjectionId, _subscriptionDispatcher);
        }
    }
}
