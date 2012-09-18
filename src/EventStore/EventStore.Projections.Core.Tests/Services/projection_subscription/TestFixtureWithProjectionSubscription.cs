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
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.Tests.Fakes;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription
{
    public abstract class TestFixtureWithProjectionSubscription
    {
        private Guid _projectionCorrelationId;
        protected TestMessageHandler<ProjectionMessage.Projections.CommittedEventReceived> _eventHandler;
        protected TestMessageHandler<ProjectionMessage.Projections.CheckpointSuggested> _checkpointHandler;
        protected ProjectionSubscription _subscription;
        protected EventDistributionPoint _forkedDistributionPoint;
        protected FakePublisher _bus;
        protected Action<QuerySourceProcessingStrategyBuilder> _source = null;
        private int _checkpointUnhandledBytesThreshold;

        [SetUp]
        public void setup()
        {
            _checkpointUnhandledBytesThreshold = 1000;
            Given();
            _bus = new FakePublisher();
            _projectionCorrelationId = Guid.NewGuid();
            _eventHandler = new TestMessageHandler<ProjectionMessage.Projections.CommittedEventReceived>();
            _checkpointHandler = new TestMessageHandler<ProjectionMessage.Projections.CheckpointSuggested>();
            _subscription = new ProjectionSubscription(
                _projectionCorrelationId, CheckpointTag.FromPosition(0, -1), _eventHandler, _checkpointHandler,
                CreateCheckpointStrategy(), _checkpointUnhandledBytesThreshold);


            When();
        }

        protected virtual void Given()
        {
        }

        protected abstract void When();

        private CheckpointStrategy CreateCheckpointStrategy()
        {
            var result = new CheckpointStrategy.Builder();
            if (_source != null)
            {
                _source(result);
            }
            else
            {
                result.FromAll();
                result.AllEvents();
            }
            return result.Build(ProjectionMode.Persistent);
        }
    }
}
