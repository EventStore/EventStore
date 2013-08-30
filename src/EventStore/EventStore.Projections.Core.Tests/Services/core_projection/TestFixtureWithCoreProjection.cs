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
using EventStore.Core.Messages;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    public abstract class TestFixtureWithCoreProjection : TestFixtureWithExistingEvents
    {
        protected CoreProjection _coreProjection;
        protected TestHandler<ReaderSubscriptionManagement.Subscribe> _subscribeProjectionHandler;
        protected TestHandler<ClientMessage.WriteEvents> _writeEventHandler;
        protected Guid _firstWriteCorrelationId;
        protected FakeProjectionStateHandler _stateHandler;
        protected int _checkpointHandledThreshold = 5;
        protected int _checkpointUnhandledBytesThreshold = 10000;
        protected Action<SourceDefinitionBuilder> _configureBuilderByQuerySource = null;
        protected Guid _projectionCorrelationId;
        private bool _createTempStreams = false;
        private ProjectionConfig _projectionConfig;
        protected ProjectionVersion _version;
        protected string _projectionName;

        protected override void Given1()
        {
            _version = new ProjectionVersion(1, 0, 0);
            _projectionName = "projection";
        }

        [SetUp]
        public void setup()
        {
            _subscribeProjectionHandler = new TestHandler<ReaderSubscriptionManagement.Subscribe>();
            _writeEventHandler = new TestHandler<ClientMessage.WriteEvents>();
            _bus.Subscribe(_subscribeProjectionHandler);
            _bus.Subscribe(_writeEventHandler);


            _stateHandler = GivenProjectionStateHandler();
            _firstWriteCorrelationId = Guid.NewGuid();
            _projectionCorrelationId = Guid.NewGuid();
            _projectionConfig = GivenProjectionConfig();
            var projectionProcessingStrategy = GivenProjectionProcessingStrategy();
            _coreProjection = GivenCoreProjection(projectionProcessingStrategy);
            _bus.Subscribe<CoreProjectionProcessingMessage.CheckpointCompleted>(_coreProjection);
            _bus.Subscribe<CoreProjectionProcessingMessage.CheckpointLoaded>(_coreProjection);
            _bus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(_coreProjection);
            _bus.Subscribe<CoreProjectionProcessingMessage.RestartRequested>(_coreProjection);
            _bus.Subscribe<CoreProjectionProcessingMessage.Failed>(_coreProjection);
            _bus.Subscribe(new AdHocHandler<ProjectionCoreServiceMessage.CoreTick>(tick => tick.Action()));
            _bus.Subscribe(new AdHocHandler<ReaderCoreServiceMessage.ReaderTick>(tick => tick.Action()));
            PreWhen();
            When();
        }

        protected virtual CoreProjection GivenCoreProjection(ProjectionProcessingStrategy projectionProcessingStrategy)
        {
            return projectionProcessingStrategy.Create(
                _projectionCorrelationId, _bus, _ioDispatcher, _subscriptionDispatcher, _timeProvider);
        }

        protected virtual ProjectionProcessingStrategy GivenProjectionProcessingStrategy()
        {
            return CreateProjectionProcessingStrategy();
        }

        protected ProjectionProcessingStrategy CreateProjectionProcessingStrategy()
        {
            return new ContinuousProjectionProcessingStrategy(
                _projectionName, _version, _stateHandler, _projectionConfig, _stateHandler.GetSourceDefinition(), null);
        }

        protected ProjectionProcessingStrategy CreateQueryProcessingStrategy()
        {
            return new QueryProcessingStrategy(
                _projectionName, _version, _stateHandler, _projectionConfig, _stateHandler.GetSourceDefinition(), null);
        }

        protected virtual ProjectionConfig GivenProjectionConfig()
        {
            return new ProjectionConfig(
                null, _checkpointHandledThreshold, _checkpointUnhandledBytesThreshold, GivenPendingEventsThreshold(),
                GivenMaxWriteBatchLength(), GivenEmitEventEnabled(), GivenCheckpointsEnabled(), _createTempStreams,
                GivenStopOnEof());
        }

        protected virtual int GivenMaxWriteBatchLength()
        {
            return 250;
        }

        protected virtual int GivenPendingEventsThreshold()
        {
            return 1000;
        }

        protected virtual bool GivenStopOnEof()
        {
            return false;
        }

        protected virtual bool GivenCheckpointsEnabled()
        {
            return true;
        }

        protected virtual bool GivenEmitEventEnabled()
        {
            return true;
        }

        protected virtual FakeProjectionStateHandler GivenProjectionStateHandler()
        {
            return new FakeProjectionStateHandler(configureBuilder: _configureBuilderByQuerySource);
        }

        protected new virtual void PreWhen()
        {
        }

        protected new abstract void When();
    }
}
