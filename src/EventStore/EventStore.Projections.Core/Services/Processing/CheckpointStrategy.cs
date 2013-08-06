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

namespace EventStore.Projections.Core.Services.Processing
{
    public class CheckpointStrategy
    {
        private readonly bool _byStream;
        private readonly bool _byCustomPartitions;
        private readonly bool _useCheckpoints;
        internal readonly bool _definesStateTransform;
        private readonly IReaderStrategy _readerStrategy;
        private readonly IPrincipal _runAs;

        public class Builder : QuerySourceProcessingStrategyBuilder
        {
            public CheckpointStrategy Build(ProjectionConfig config, IPrincipal runAs, IReaderStrategy readerStrategy)
            {
                base.Validate();
                return new CheckpointStrategy(
                    _byStream, _byCustomPartitions, config.CheckpointsEnabled, _definesStateTransform, runAs, readerStrategy);
            }

            public void Validate(ProjectionConfig config)
            {
                base.Validate();
                if (_definesStateTransform && !config.EmitEventEnabled)
                    throw new InvalidOperationException("transformBy/filterBy requires EmitEventEnabled mode");
            }
        }

        public static CheckpointStrategy Create(
            int phase, ISourceDefinitionConfigurator sources, ProjectionConfig config, ITimeProvider timeProvider)
        {
            var builder = new Builder();
            sources.ConfigureSourceProcessingStrategy(builder);
            return builder.Build(
                config, config.RunAs, Processing.ReaderStrategy.Create(phase, sources, timeProvider, config.RunAs));
        }

        public bool UseCheckpoints
        {
            get { return _useCheckpoints; }
        }

        public IReaderStrategy ReaderStrategy
        {
            get { return _readerStrategy; }
        }

        private CheckpointStrategy(
            bool byStream, bool byCustomPartitions, bool useCheckpoints, bool definesStateTransform, IPrincipal runAs,
            IReaderStrategy readerStrategy)
        {
            _readerStrategy = readerStrategy;
            _byStream = byStream;
            _byCustomPartitions = byCustomPartitions;
            _useCheckpoints = useCheckpoints;
            _definesStateTransform = definesStateTransform;
            _runAs = runAs;
        }

        public StatePartitionSelector CreateStatePartitionSelector(IProjectionStateHandler projectionStateHandler)
        {
            return _byCustomPartitions
                       ? new ByHandleStatePartitionSelector(projectionStateHandler)
                       : (_byStream
                              ? (StatePartitionSelector) new ByStreamStatePartitionSelector()
                              : new NoopStatePartitionSelector());
        }

        public ICoreProjectionCheckpointManager CreateCheckpointManager(
            Guid projectionCorrelationId, ProjectionVersion projectionVersion, IPublisher publisher,
            IODispatcher ioDispatcher, ProjectionConfig projectionConfig, string name,
            ProjectionNamesBuilder namingBuilder)
        {
            var emitAny = projectionConfig.EmitEventEnabled;
            var emitPartitionCheckpoints = UseCheckpoints && (_byCustomPartitions || _byStream);

            //NOTE: not emitting one-time/transient projections are always handled by default checkpoint manager
            // as they don't depend on stable event order
            if (emitAny && !ReaderStrategy.IsReadingOrderRepeatable)
            {
                return new MultiStreamMultiOutputCheckpointManager(
                    publisher, projectionCorrelationId, projectionVersion, _runAs, ioDispatcher, projectionConfig, name,
                    ReaderStrategy.PositionTagger, namingBuilder, UseCheckpoints,
                    emitPartitionCheckpoints);
            }
            else
            {
                return new DefaultCheckpointManager(
                    publisher, projectionCorrelationId, projectionVersion, _runAs, ioDispatcher, projectionConfig, name,
                    ReaderStrategy.PositionTagger, namingBuilder, UseCheckpoints, emitPartitionCheckpoints);
            }
        }

        public IResultEmitter CreateResultEmitter(ProjectionNamesBuilder namingBuilder)
        {
            var resultEmitter = _definesStateTransform
                ? new ResultEmitter(namingBuilder)
                : (IResultEmitter) new NoopResultEmitter();
            return resultEmitter;
        }
    }
}
