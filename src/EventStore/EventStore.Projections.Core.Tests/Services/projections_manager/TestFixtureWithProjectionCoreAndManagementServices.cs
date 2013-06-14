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
        protected ProjectionManager _manager;
        private ProjectionCoreService _coreService;
        private EventReaderCoreService _readerService;
        private bool _initializeSystemProjections;

        protected override void Given1()
        {
            base.Given1();
            _initializeSystemProjections = GivenInitializeSystemProjections();
            if (!_initializeSystemProjections)
            {
                ExistingEvent("$projections-$all", "$ProjectionsInitialized", "", "");
            }
        }

        protected virtual bool GivenInitializeSystemProjections()
        {
            return false;
        }

        [SetUp]
        public void Setup()
        {
            SetUpManualQueue();
            //TODO: this became an integration test - proper ProjectionCoreService and ProjectionManager testing is required as well
            _bus.Subscribe(_consumer);

            _manager = new ProjectionManager(
                GetInputQueue(), GetInputQueue(), new[] {GetInputQueue()}, _timeProvider, true,
                _initializeSystemProjections);
            ICheckpoint writerCheckpoint = new InMemoryCheckpoint(1000);
            _readerService = new EventReaderCoreService(GetInputQueue(), 10, writerCheckpoint, runHeadingReader: true);
            _subscriptionDispatcher =
                new PublishSubscribeDispatcher
                    <ReaderSubscriptionManagement.Subscribe,
                        ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage
                        >(GetInputQueue(), v => v.SubscriptionId, v => v.SubscriptionId);

            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());

            _coreService = new ProjectionCoreService(GetInputQueue(), GetInputQueue(), _subscriptionDispatcher, _timeProvider);
            _bus.Subscribe<ProjectionManagementMessage.Internal.CleanupExpired>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Internal.Deleted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Started>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Stopped>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Prepared>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Faulted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StateReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.ResultReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StatisticsReport>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Post>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.UpdateQuery>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetQuery>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Delete>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetStatistics>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetState>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetResult>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Disable>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Enable>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.SetRunAs>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Reset>(_manager);
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
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderNotAuthorized>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Pause>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Resume>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Subscribe>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(_readerService);
            
            Given();
            WhenLoop();
        }

    }
}
