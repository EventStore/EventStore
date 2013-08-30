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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection.checkpoint_manager
{
    public class TestFixtureWithCoreProjectionCheckpointManager : TestFixtureWithExistingEvents
    {
        protected DefaultCheckpointManager _manager;
        protected FakeCoreProjection _projection;
        protected ProjectionConfig _config;
        protected int _checkpointHandledThreshold;
        protected int _checkpointUnhandledBytesThreshold;
        protected int _pendingEventsThreshold;
        protected int _maxWriteBatchLength;
        protected bool _emitEventEnabled;
        protected bool _checkpointsEnabled;
        protected bool _outputRunningResults;
        protected Guid _projectionCorrelationId;
        private string _projectionCheckpointStreamId;
        protected bool _createTempStreams;
        protected bool _stopOnEof;
        protected ProjectionNamesBuilder _namingBuilder;
        protected ResultEmitter _resultEmitter;
        protected CoreProjectionCheckpointWriter _checkpointWriter;
        protected CoreProjectionCheckpointReader _checkpointReader;
        protected string _projectionName;
        protected ProjectionVersion _projectionVersion;

        [SetUp]
        public void setup()
        {
            Given();
            _namingBuilder = ProjectionNamesBuilder.CreateForTest("projection");
            _config = new ProjectionConfig(null, _checkpointHandledThreshold, _checkpointUnhandledBytesThreshold,
                _pendingEventsThreshold, _maxWriteBatchLength, _emitEventEnabled,
                _checkpointsEnabled, _createTempStreams, _stopOnEof);
            _resultEmitter = new ResultEmitter(_namingBuilder);
            When();
        }

        protected new virtual void When()
        {
            _projectionVersion = new ProjectionVersion(1, 0, 0);
            _projectionName = "projection";
            _checkpointWriter = new CoreProjectionCheckpointWriter(
                _namingBuilder.MakeCheckpointStreamName(), _ioDispatcher, _projectionVersion, _projectionName);
            _checkpointReader = new CoreProjectionCheckpointReader(
                GetInputQueue(), _projectionCorrelationId, _ioDispatcher, _projectionCheckpointStreamId,
                _projectionVersion, _checkpointsEnabled);
            _manager = GivenCheckpointManager();
        }

        protected virtual DefaultCheckpointManager GivenCheckpointManager()
        {
            return new DefaultCheckpointManager(
                _bus, _projectionCorrelationId, _projectionVersion, null, _ioDispatcher, _config, _projectionName,
                new StreamPositionTagger(0, "stream"), _namingBuilder, _checkpointsEnabled, _outputRunningResults,
                _checkpointWriter);
        }

        protected new virtual void Given()
        {
            _projectionCheckpointStreamId = "$projections-projection-checkpoint";
            _projectionCorrelationId = Guid.NewGuid();
            _projection = new FakeCoreProjection();
            _bus.Subscribe<CoreProjectionProcessingMessage.CheckpointCompleted>(_projection);
            _bus.Subscribe<CoreProjectionProcessingMessage.CheckpointLoaded>(_projection);
            _bus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(_projection);
            _bus.Subscribe<CoreProjectionProcessingMessage.RestartRequested>(_projection);
            _bus.Subscribe<CoreProjectionProcessingMessage.Failed>(_projection);
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            _checkpointHandledThreshold = 2;
            _checkpointUnhandledBytesThreshold = 5;
            _pendingEventsThreshold = 5;
            _maxWriteBatchLength = 5;
            _emitEventEnabled = true;
            _checkpointsEnabled = true;
            _outputRunningResults = true;
            _createTempStreams = false;
            _stopOnEof = false;
            NoStream(_projectionCheckpointStreamId);
        }
    }
}
