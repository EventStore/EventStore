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
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.core_projection_checkpoint_manager
{
    public class TestFixtureWithCoreProjectionCheckpointManager : TestFixtureWithExistingEvents
    {
        protected CoreProjectionDefaultCheckpointManager _manager;
        protected FakeCoreProjection _projection;
        protected ProjectionConfig _config;
        protected ProjectionMode _projectionMode;
        protected int _checkpointHandledThreshold;
        protected int _checkpointUnhandledBytesThreshold;
        protected int _pendingEventsThreshold;
        protected int _maxWriteBatchLength;
        protected bool _publishStateUpdates;
        protected bool _emitEventEnabled;
        protected bool _checkpointsEnabled;
        protected Guid _projectionCorrelationId;
        private string _projectionCheckpointStreamId;

        [SetUp]
        public void setup()
        {
            Given();
            _config = new ProjectionConfig(
                _projectionMode, _checkpointHandledThreshold, _checkpointUnhandledBytesThreshold,
                _pendingEventsThreshold, _maxWriteBatchLength, _publishStateUpdates, _emitEventEnabled,
                _checkpointsEnabled);
            When();
        }

        protected virtual void When()
        {
            _manager = new CoreProjectionDefaultCheckpointManager(
                _projection, _bus, _projectionCorrelationId, _readDispatcher, _writeDispatcher, _config, _projectionCheckpointStreamId, "projection",
                new StreamPositionTagger("stream"));
        }

        protected virtual void Given()
        {
            _projectionCheckpointStreamId = "$projections-projection-checkpoint";
            _projectionCorrelationId = Guid.NewGuid();
            _projection = new FakeCoreProjection();
            _projectionMode = ProjectionMode.Persistent;
            _checkpointHandledThreshold = 2;
            _checkpointUnhandledBytesThreshold = 5;
            _pendingEventsThreshold = 5;
            _maxWriteBatchLength = 5;
            _publishStateUpdates = true;
            _emitEventEnabled = true;
            _checkpointsEnabled = true;
            NoStream(_projectionCheckpointStreamId);
        }
    }
}
