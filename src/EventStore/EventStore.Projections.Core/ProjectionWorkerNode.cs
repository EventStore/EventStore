using System;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Common.Utils;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using EventStore.Projections.Core.EventReaders.Feeds;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core
{
    public class ProjectionWorkerNode 
    {
        private readonly RunProjections _runProjections;
        private readonly ProjectionCoreService _projectionCoreService;
        private readonly ProjectionCoreServiceCommandReader _projectionCoreServiceCommandReader;
        private readonly InMemoryBus _coreOutput;
        private readonly EventReaderCoreService _eventReaderCoreService;

        private readonly ReaderSubscriptionDispatcher _subscriptionDispatcher;

        private readonly FeedReaderService _feedReaderService;
        private readonly IODispatcher _ioDispatcher;

        private readonly SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;
        private ResponseWriter _responseWriter;
        private ProjectionCoreResponseWriter _coreResponseWriter;

        public ProjectionWorkerNode(
            Guid workerId,
            TFChunkDb db,
            QueuedHandler inputQueue,
            ITimeProvider timeProvider,
            ISingletonTimeoutScheduler timeoutScheduler,
            RunProjections runProjections)
        {
            _runProjections = runProjections;
            Ensure.NotNull(db, "db");

            _coreOutput = new InMemoryBus("Core Output");

            IPublisher publisher = CoreOutput;
            _subscriptionDispatcher = new ReaderSubscriptionDispatcher(publisher);
            _spoolProcessingResponseDispatcher = new SpooledStreamReadingDispatcher(publisher);

            _ioDispatcher = new IODispatcher(publisher, new PublishEnvelope(inputQueue));
            _eventReaderCoreService = new EventReaderCoreService(
                publisher,
                _ioDispatcher,
                10,
                db.Config.WriterCheckpoint,
                runHeadingReader: runProjections >= RunProjections.System);

            _feedReaderService = new FeedReaderService(_subscriptionDispatcher, timeProvider);
            if (runProjections >= RunProjections.System)
            {
                _projectionCoreServiceCommandReader = new ProjectionCoreServiceCommandReader(
                    publisher,
                    _ioDispatcher,
                    workerId.ToString("N"));

                _projectionCoreService = new ProjectionCoreService(
                    workerId,
                    inputQueue,
                    publisher,
                    _subscriptionDispatcher,
                    timeProvider,
                    _ioDispatcher,
                    _spoolProcessingResponseDispatcher,
                    timeoutScheduler);

                _responseWriter = new ResponseWriter(_ioDispatcher);
                _coreResponseWriter = new ProjectionCoreResponseWriter(_responseWriter);
            }
        }

        public InMemoryBus CoreOutput
        {
            get { return _coreOutput; }
        }

        public void SetupMessaging(IBus coreInputBus)
        {
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionDeleted>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
            coreInputBus.Subscribe(_spoolProcessingResponseDispatcher.CreateSubscriber<PartitionProcessingResult>());
            coreInputBus.Subscribe(_spoolProcessingResponseDispatcher.CreateSubscriber<PartitionMeasured>());
            coreInputBus.Subscribe(_spoolProcessingResponseDispatcher.CreateSubscriber<PartitionProcessingProgress>());

            coreInputBus.Subscribe(_feedReaderService);

            if (_runProjections >= RunProjections.System)
            {

                coreInputBus.Subscribe<ProjectionCoreServiceMessage.StartCore>(_projectionCoreService);
                coreInputBus.Subscribe<ProjectionCoreServiceMessage.StopCore>(_projectionCoreService);
                coreInputBus.Subscribe<ProjectionCoreServiceMessage.StartCore>(_projectionCoreServiceCommandReader);
                coreInputBus.Subscribe<ProjectionCoreServiceMessage.StopCore>(_projectionCoreServiceCommandReader);
                coreInputBus.Subscribe<ProjectionCoreServiceMessage.CoreTick>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepare>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.CreatePrepared>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepareSlave>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.Dispose>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.Start>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.LoadStopped>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.Stop>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.Kill>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.GetState>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.GetResult>(_projectionCoreService);
                coreInputBus.Subscribe<ProjectionManagementMessage.SlaveProjectionsStarted>(_projectionCoreService);
                coreInputBus.Subscribe<ClientMessage.ReadStreamEventsForwardCompleted>(_ioDispatcher.ForwardReader);
                coreInputBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_ioDispatcher.BackwardReader);
                coreInputBus.Subscribe<ClientMessage.WriteEventsCompleted>(_ioDispatcher.Writer);
                coreInputBus.Subscribe<ClientMessage.DeleteStreamCompleted>(_ioDispatcher.StreamDeleter);
                coreInputBus.Subscribe<IODispatcherDelayedMessage>(_ioDispatcher.Awaker);
                coreInputBus.Subscribe<IODispatcherDelayedMessage>(_ioDispatcher);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.CheckpointCompleted>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.CheckpointLoaded>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.RestartRequested>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.Failed>(_projectionCoreService);
                //NOTE: message forwarding is set up outside (for Read/Write events)
            }

            coreInputBus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderCoreServiceMessage.StartReader>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderCoreServiceMessage.StopReader>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Subscribe>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Pause>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Resume>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.SpoolStreamReadingCore>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.CompleteSpooledStreamReading>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.CommittedEventDistributed>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderStarting>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderEof>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionEof>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionDeleted>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionMeasured>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderNotAuthorized>(_eventReaderCoreService);
            //NOTE: message forwarding is set up outside (for Read/Write events)

            coreInputBus.Subscribe<CoreProjectionStatusMessage.Faulted>(_coreResponseWriter);
            coreInputBus.Subscribe<CoreProjectionStatusMessage.Prepared>(_coreResponseWriter);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>(_coreResponseWriter);
            coreInputBus.Subscribe<CoreProjectionStatusMessage.Started>(_coreResponseWriter);
            coreInputBus.Subscribe<CoreProjectionStatusMessage.StatisticsReport>(_coreResponseWriter);
            coreInputBus.Subscribe<CoreProjectionStatusMessage.Stopped>(_coreResponseWriter);
            coreInputBus.Subscribe<CoreProjectionStatusMessage.StateReport>(_coreResponseWriter);
            coreInputBus.Subscribe<CoreProjectionStatusMessage.ResultReport>(_coreResponseWriter);

        }
    }
}