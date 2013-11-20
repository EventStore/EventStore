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
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Data.ReadStreamResult;
using ResolvedEvent = EventStore.Core.Data.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_starting_a_projection
    {
        private const string _projectionStateStream = "$projections-projection-result";
        private const string _projectionCheckpointStream = "$projections-projection-checkpoint";
        private CoreProjection _coreProjection;
        private InMemoryBus _bus;
        private TestHandler<ClientMessage.ReadStreamEventsBackward> _listEventsHandler;
        private IODispatcher _ioDispatcher;
        private ReaderSubscriptionDispatcher _subscriptionDispatcher;
        private ProjectionConfig _projectionConfig;

        [SetUp]
        public void setup()
        {
            _bus = new InMemoryBus("bus");
            _listEventsHandler = new TestHandler<ClientMessage.ReadStreamEventsBackward>();
            _bus.Subscribe(_listEventsHandler);
            _ioDispatcher = new IODispatcher(_bus, new PublishEnvelope(_bus));
            _subscriptionDispatcher =
                new ReaderSubscriptionDispatcher
                    (_bus);
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
            _bus.Subscribe(_ioDispatcher.BackwardReader);
            _bus.Subscribe(_ioDispatcher.ForwardReader);
            _bus.Subscribe(_ioDispatcher.Writer);
            _bus.Subscribe(_ioDispatcher);
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            _projectionConfig = new ProjectionConfig(null, 5, 10, 1000, 250, true, true, false, false, false);
            var version = new ProjectionVersion(1, 0, 0);
            var projectionProcessingStrategy = new ContinuousProjectionProcessingStrategy(
                "projection", version, projectionStateHandler, _projectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher);
            _coreProjection = projectionProcessingStrategy.Create(
                Guid.NewGuid(), _bus, SystemAccount.Principal, _bus, _ioDispatcher, _subscriptionDispatcher,
                new RealTimeProvider());
            _coreProjection.Start();
        }

        [Test]
        public void should_request_state_snapshot()
        {
            Assert.IsTrue(_listEventsHandler.HandledMessages.Count == 1);
        }

        [Test]
        public void should_request_state_snapshot_on_correct_stream()
        {
            Assert.AreEqual(_projectionCheckpointStream, _listEventsHandler.HandledMessages[0].EventStreamId);
        }

        [Test]
        public void should_accept_no_event_stream_response()
        {
            _bus.Handle(
                new ClientMessage.ReadStreamEventsBackwardCompleted(
                    _listEventsHandler.HandledMessages[0].CorrelationId,
                    _listEventsHandler.HandledMessages[0].EventStreamId, 100, 100, ReadStreamResult.NoStream,
                    new ResolvedEvent[0], null, false, string.Empty, -1, -1, true, 1000));
        }

        [Test]
        public void should_accept_events_not_found_response()
        {
            _bus.Handle(
                new ClientMessage.ReadStreamEventsBackwardCompleted(
                    _listEventsHandler.HandledMessages[0].CorrelationId,
                    _listEventsHandler.HandledMessages[0].EventStreamId, 100, 100, ReadStreamResult.Success,
                    new ResolvedEvent[0], null, false, string.Empty, -1, -1, false, 1000));
        }
    }
}
