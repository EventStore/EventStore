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

using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public abstract class TestFixtureWithProjectionCoreAndManagementServices : TestFixtureWithExistingEvents
    {
        protected FakeTimeProvider _timeProvider;

        protected ProjectionManager _manager;
        private ProjectionCoreService _coreService;
        private EventReaderCoreService _readerService;

        protected override void Given1()
        {
            base.Given1();
            ExistingEvent("$projections-$all", "$ProjectionsInitialized", "", "");
        }

        [SetUp]
        public void Setup()
        {
            _timeProvider = new FakeTimeProvider();
            //TODO: this became an integration test - proper ProjectionCoreService and ProjectionManager testing is required as well
            _bus.Subscribe(_consumer);

            _manager = new ProjectionManager(_bus, _bus, new IPublisher[] {_bus}, _timeProvider, true);
            ICheckpoint writerCheckpoint = new InMemoryCheckpoint(1000);
            _readerService = new EventReaderCoreService(_bus, 10, writerCheckpoint, runHeadingReader: true);
            _subscriptionDispatcher =
                new PublishSubscribeDispatcher
                    <ReaderSubscriptionManagement.Subscribe,
                        ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage
                        >(_bus, v => v.SubscriptionId, v => v.SubscriptionId);

            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());

            _coreService = new ProjectionCoreService(_bus, _bus, _subscriptionDispatcher);
            _bus.Subscribe<ProjectionManagementMessage.Internal.CleanupExpired>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Internal.Deleted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Started>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Stopped>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Prepared>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Faulted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StateReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.ResultReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StatisticsReport>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<SystemMessage.StateChangeMessage>(_manager);

            _bus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepare>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.CreatePrepared>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.Dispose>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.Start>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.LoadStopped>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.Stop>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.Kill>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.GetState>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.GetResult>(_coreService);
            _bus.Subscribe<CoreProjectionManagementMessage.UpdateStatistics>(_coreService);
            _bus.Subscribe<CoreProjectionProcessingMessage.CheckpointCompleted>(_coreService);
            _bus.Subscribe<CoreProjectionProcessingMessage.CheckpointLoaded>(_coreService);
            _bus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(_coreService);
            _bus.Subscribe<CoreProjectionProcessingMessage.RestartRequested>(_coreService);
            _bus.Subscribe<CoreProjectionProcessingMessage.Failed>(_coreService);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_coreService);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_coreService);
            _bus.Subscribe<ProjectionCoreServiceMessage.StartCore>(_coreService);
            _bus.Subscribe<ProjectionCoreServiceMessage.StopCore>(_coreService);
            _bus.Subscribe<ReaderCoreServiceMessage.StartReader>(_readerService);
            _bus.Subscribe<ReaderCoreServiceMessage.StopReader>(_readerService);
            _bus.Subscribe<ProjectionCoreServiceMessage.CoreTick>(_coreService);
            _bus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.CommittedEventDistributed>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderEof>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Pause>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Resume>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Subscribe>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(_readerService);
            
            Given();
            When();
        }

        protected abstract void When();
    }
}
