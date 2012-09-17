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
        protected TestMessageHandler<ProjectionMessage.Projections.SubscribeProjection> _subscribeProjectionHandler;
        protected TestMessageHandler<ClientMessage.WriteEvents> _writeEventHandler;
        protected readonly string _lastSeenEvent = Guid.NewGuid().ToString("D");

        protected Guid _firstWriteCorrelationId;
        protected FakeProjectionStateHandler _stateHandler;
        protected int _checkpointHandledThreshold = 5;
        protected int _checkpointUnhandledBytesThreshold = 10000;
        protected Action<QuerySourceProcessingStrategyBuilder> _configureBuilderByQuerySource = null;
        private Guid _projectionCorrelationId;
        private ProjectionMode _projectionMode = ProjectionMode.Persistent;


        //TODO: move this to base class
        [SetUp]
        public void setup()
        {
            _subscribeProjectionHandler = new TestMessageHandler<ProjectionMessage.Projections.SubscribeProjection>();
            _writeEventHandler = new TestMessageHandler<ClientMessage.WriteEvents>();
            _bus.Subscribe(_subscribeProjectionHandler);
            _bus.Subscribe(_writeEventHandler);


            _stateHandler = _stateHandler
                            ?? new FakeProjectionStateHandler(configureBuilder: _configureBuilderByQuerySource);
            _firstWriteCorrelationId = Guid.NewGuid();
            _projectionCorrelationId = Guid.NewGuid();
            _coreProjection = new CoreProjection(
                "projection", _projectionCorrelationId, _bus, _stateHandler,
                new ProjectionConfig(
                    _projectionMode, _checkpointHandledThreshold, _checkpointUnhandledBytesThreshold, 1000, 250, true, true,
                    true));
            _coreProjection.Start();

            When();
        }

        protected abstract void When();
    }
}
