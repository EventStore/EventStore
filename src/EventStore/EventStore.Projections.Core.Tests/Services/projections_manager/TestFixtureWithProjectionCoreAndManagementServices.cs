using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
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
        protected ProjectionManagerMessageDispatcher _managerMessageDispatcher;
        private bool _initializeSystemProjections;
        protected Tuple<IBus, IPublisher, InMemoryBus, TimeoutScheduler, Guid>[] _processingQueues;

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
            return new ManualQueue(_bus, _timeProvider);
        }

        [SetUp]
        public void Setup()
        {
            //TODO: this became an integration test - proper ProjectionCoreService and ProjectionManager testing is required as well
            _bus.Subscribe(_consumer);

            _processingQueues = GivenProcessingQueues();
            var queues = _processingQueues.ToDictionary(v => v.Item5, v => (IPublisher)v.Item1);
            _managerMessageDispatcher = new ProjectionManagerMessageDispatcher(queues);
            _manager = new ProjectionManager(
                GetInputQueue(),
                GetInputQueue(),
                queues,
                _timeProvider,
                RunProjections.All,
                ProjectionManagerNode.CreateTimeoutSchedulers(queues.Count),
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
            _bus.Subscribe<CoreProjectionManagementMessage.ProjectionWorkerStarted>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Post>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.UpdateQuery>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetQuery>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Delete>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetStatistics>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetState>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetResult>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Disable>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Enable>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Abort>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.SetRunAs>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Reset>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.StartSlaveProjections>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<SystemMessage.StateChangeMessage>(_manager);

            if (GetInputQueue() != _processingQueues.First().Item2)
            {
                _bus.Subscribe<PartitionProcessingResultBase>(_managerMessageDispatcher);
                _bus.Subscribe<ReaderSubscriptionManagement.SpoolStreamReading>(_managerMessageDispatcher);
                _bus.Subscribe<CoreProjectionManagementMessage.CoreProjectionManagementControlMessage>(
                    _managerMessageDispatcher);
            }

            foreach(var q in _processingQueues)
                SetUpCoreServices(q.Item5, q.Item1, q.Item2, q.Item3, q.Item4);

            //Given();
            WhenLoop();
        }

        protected virtual Tuple<IBus, IPublisher, InMemoryBus, TimeoutScheduler, Guid>[] GivenProcessingQueues()
        {
            return new[]
            {Tuple.Create((IBus) _bus, GetInputQueue(), (InMemoryBus) null, default(TimeoutScheduler), Guid.NewGuid())};
        }

        private void SetUpCoreServices(
            Guid workerId,
            IBus bus,
            IPublisher inputQueue,
            InMemoryBus output_,
            ISingletonTimeoutScheduler timeoutScheduler)
        {
            var output = (output_ ?? inputQueue);
            ICheckpoint writerCheckpoint = new InMemoryCheckpoint(1000);
            var readerService = new EventReaderCoreService(
                output,
                _ioDispatcher,
                10,
                writerCheckpoint,
                runHeadingReader: true);
            _subscriptionDispatcher = new ReaderSubscriptionDispatcher(inputQueue);
            var spoolProcessingResponseDispatcher = new SpooledStreamReadingDispatcher(GetInputQueue());

            bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionDeleted>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
            bus.Subscribe(spoolProcessingResponseDispatcher.CreateSubscriber<PartitionProcessingResult>());

            var ioDispatcher = new IODispatcher(output, new PublishEnvelope(inputQueue));
//            var coreServiceCommandReader = new ProjectionCoreServiceCommandReader(
//                output,
//                ioDispatcher,
//                workerId.ToString("N"));

            var coreService = new ProjectionCoreService(
                workerId,
                inputQueue,
                output,
                _subscriptionDispatcher,
                _timeProvider,
                ioDispatcher,
                spoolProcessingResponseDispatcher,
                timeoutScheduler);

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
            bus.Subscribe<CoreProjectionProcessingMessage.CheckpointCompleted>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.CheckpointLoaded>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.RestartRequested>(coreService);
            bus.Subscribe<CoreProjectionProcessingMessage.Failed>(coreService);
            bus.Subscribe<ClientMessage.ReadStreamEventsForwardCompleted>(ioDispatcher.ForwardReader);
            bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(ioDispatcher.BackwardReader);
            bus.Subscribe<ClientMessage.WriteEventsCompleted>(ioDispatcher.Writer);
            bus.Subscribe<ClientMessage.DeleteStreamCompleted>(ioDispatcher.StreamDeleter);
            bus.Subscribe<IODispatcherDelayedMessage>(ioDispatcher.Awaker);
            bus.Subscribe<IODispatcherDelayedMessage>(ioDispatcher);
            bus.Subscribe<ProjectionCoreServiceMessage.StartCore>(coreService);
            bus.Subscribe<ProjectionCoreServiceMessage.StopCore>(coreService);
//            bus.Subscribe<ProjectionCoreServiceMessage.StartCore>(coreServiceCommandReader);
//            bus.Subscribe<ProjectionCoreServiceMessage.StopCore>(coreServiceCommandReader);
            bus.Subscribe<ReaderCoreServiceMessage.StartReader>(readerService);
            bus.Subscribe<ReaderCoreServiceMessage.StopReader>(readerService);
            bus.Subscribe<ProjectionCoreServiceMessage.CoreTick>(coreService);
            bus.Subscribe<ProjectionManagementMessage.SlaveProjectionsStarted>(coreService);
            bus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.CommittedEventDistributed>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderEof>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionEof>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionDeleted>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionMeasured>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderNotAuthorized>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(readerService);
            bus.Subscribe<ReaderSubscriptionMessage.EventReaderStarting>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.Pause>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.Resume>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.Subscribe>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(readerService);
            bus.Subscribe<ReaderSubscriptionManagement.SpoolStreamReadingCore>(readerService);
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
                output_.Subscribe(Forwarder.Create<CoreProjectionManagementMessage.ProjectionWorkerStarted>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<ProjectionManagementMessage.ControlMessage>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<AwakeServiceMessage.SubscribeAwake>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<AwakeServiceMessage.UnsubscribeAwake>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<PartitionProcessingResultBase>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<ReaderSubscriptionManagement.SpoolStreamReading>(GetInputQueue()));
                output_.Subscribe(Forwarder.Create<Message>(inputQueue)); // forward all

                var forwarder = new RequestResponseQueueForwarder(
                    inputQueue: inputQueue,
                    externalRequestQueue: GetInputQueue());
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
