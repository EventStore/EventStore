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
using System.Linq;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using TestFixtureWithExistingEvents = EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithExistingEvents;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public abstract class TestFixtureWithProjectionCoreAndManagementServices : TestFixtureWithExistingEvents
    {
        protected ProjectionManager _manager;
        private bool _initializeSystemProjections;
        protected Tuple<IBus, IPublisher, InMemoryBus>[] _processingQueues;

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

        protected override ManualQueue GiveInputQueue()
        {
            return new ManualQueue(_bus);
        }

        [SetUp]
        public void Setup()
        {
            //TODO: this became an integration test - proper ProjectionCoreService and ProjectionManager testing is required as well
            _bus.Subscribe(_consumer);

            _processingQueues = GivenProcessingQueues();
            _manager = new ProjectionManager(
                GetInputQueue(), GetInputQueue(), _processingQueues.Select(v => v.Item1).ToArray(), _timeProvider, RunProjections.All,
                _initializeSystemProjections);

            _bus.Subscribe<ProjectionManagementMessage.Internal.CleanupExpired>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Internal.Deleted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Started>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Stopped>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Prepared>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Faulted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StateReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.ResultReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StatisticsReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.SlaveProjectionsStarted>(_manager);
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
            _bus.Subscribe<ProjectionManagementMessage.StartSlaveProjections>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<SystemMessage.StateChangeMessage>(_manager);


            foreach(var q in _processingQueues)
                SetUpCoreServices(q.Item1, q.Item2, q.Item3);

            //Given();
            WhenLoop();
        }

        protected virtual Tuple<IBus, IPublisher, InMemoryBus>[] GivenProcessingQueues()
        {
            return new[] { Tuple.Create((IBus)_bus, GetInputQueue(), (InMemoryBus)null) };
        }

        private void SetUpCoreServices(IBus bus, IPublisher inputQueue, InMemoryBus output_)
        {
            var output = (output_ ?? inputQueue);
            ICheckpoint writerCheckpoint = new InMemoryCheckpoint(1000);
            var readerService = new EventReaderCoreService(
                output, _ioDispatcher, 10, writerCheckpoint, runHeadingReader: true);
            _subscriptionDispatcher = new ReaderSubscriptionDispatcher(inputQueue);
            var spoolProcessingResponseDispatcher = new SpooledStreamReadingDispatcher(GetInputQueue());

            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
            bus.Subscribe(spoolProcessingResponseDispatcher.CreateSubscriber<PartitionProcessingResult>());

            var ioDispatcher = new IODispatcher(output, new PublishEnvelope(inputQueue));
            var coreService = new ProjectionCoreService(
                inputQueue, output, _subscriptionDispatcher, _timeProvider, ioDispatcher, spoolProcessingResponseDispatcher);

            bus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepare>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.CreatePrepared>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepareSlave>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.Dispose>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.Start>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.LoadStopped>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.Stop>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.Kill>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.GetState>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.GetResult>(coreService);
            bus.Subscribe<CoreProjectionManagementMessage.UpdateStatistics>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.CheckpointCompleted>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.CheckpointLoaded>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.RestartRequested>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.Failed>(coreService);
            bus.Subscribe<ClientMessage.ReadStreamEventsForwardCompleted>(ioDispatcher.ForwardReader);
            bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(ioDispatcher.BackwardReader);
            bus.Subscribe<ClientMessage.WriteEventsCompleted>(ioDispatcher.Writer);
            bus.Subscribe<ClientMessage.DeleteStreamCompleted>(ioDispatcher.StreamDeleter);
            bus.Subscribe<IODispatcherDelayedMessage>(ioDispatcher);
            bus.Subscribe<ProjectionCoreServiceMessage.StartCore>(coreService);
            bus.Subscribe<ProjectionCoreServiceMessage.StopCore>(coreService);
            bus.Subscribe<ReaderCoreServiceMessage.StartReader>(readerService);
            bus.Subscribe<ReaderCoreServiceMessage.StopReader>(readerService);
            bus.Subscribe<ProjectionCoreServiceMessage.CoreTick>(coreService);
            bus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.CommittedEventDistributed>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderEof>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionEof>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionMeasured>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderNotAuthorized>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderStarting>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.Pause>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.Resume>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.Subscribe>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.SpoolStreamReading>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.CompleteSpooledStreamReading>(readerService);

            if (output_ != null)
            {
                bus.Subscribe(new UnwrapEnvelopeHandler());
                output_.Subscribe(Forwarder.Create<CoreProjectionManagementMessage.StateReport>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<CoreProjectionManagementMessage.ResultReport>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<CoreProjectionManagementMessage.StatisticsReport>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<CoreProjectionManagementMessage.Started>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<CoreProjectionManagementMessage.Stopped>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<CoreProjectionManagementMessage.Faulted>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<CoreProjectionManagementMessage.Prepared>(GetInputQueue()));
                output_.Subscribe(
                    Forwarder.Create<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<Message>(inputQueue)); // forward all

                var forwarder = new RequestResponseQueueForwarder(
                    inputQueue: inputQueue, externalRequestQueue: GetInputQueue());
                // forwarded messages
                output_.Subscribe<ClientMessage.ReadEvent>(forwarder);
                output_.Subscribe<ClientMessage.ReadStreamEventsBackward>(forwarder);
                output_.Subscribe<ClientMessage.ReadStreamEventsForward>(forwarder);
                output_.Subscribe<ClientMessage.ReadAllEventsForward>(forwarder);
                output_.Subscribe<ClientMessage.WriteEvents>(forwarder);

            }
        }
    }
}
