using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.EventReaders.Feeds;
using EventStore.Projections.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core
{
    public sealed class ProjectionsSubsystem : ISubsystem
    {
        private Projections _projections;
        private readonly int _projectionWorkerThreadCount;
        private readonly RunProjections _runProjections;

        public ProjectionsSubsystem(int projectionWorkerThreadCount, RunProjections runProjections)
        {
            if (runProjections <= RunProjections.System)
                _projectionWorkerThreadCount = 1;
            else
                _projectionWorkerThreadCount = projectionWorkerThreadCount;
            _runProjections = runProjections;
        }

        public void Register(
            TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService,
            ITimeProvider timeProvider, IHttpForwarder httpForwarder, HttpService[] httpServices, IPublisher networkSendService)
        {
            _projections = new EventStore.Projections.Core.Projections(
                db, mainQueue, mainBus, timerService, timeProvider, httpForwarder, httpServices, networkSendService,
                projectionWorkerThreadCount: _projectionWorkerThreadCount, runProjections: _runProjections);
        }

        public void Start()
        {
            _projections.Start();
        }

        public void Stop()
        {
            _projections.Stop();
        }
    }

    sealed class Projections
    {
        public const int VERSION = 3;

        private List<QueuedHandler> _coreQueues;
        private readonly int _projectionWorkerThreadCount;
        private QueuedHandler _managerInputQueue;
        private InMemoryBus _managerInputBus;
        private ProjectionManagerNode _projectionManagerNode;

        public Projections(
            TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService, ITimeProvider timeProvider,
            IHttpForwarder httpForwarder, HttpService[] httpServices, IPublisher networkSendQueue,
            int projectionWorkerThreadCount, RunProjections runProjections)
        {
            _projectionWorkerThreadCount = projectionWorkerThreadCount;
            SetupMessaging(
                db, mainQueue, mainBus, timerService, timeProvider, httpForwarder, httpServices, networkSendQueue,
                runProjections);

        }

        private void SetupMessaging(
            TFChunkDb db, QueuedHandler mainQueue, ISubscriber mainBus, TimerService timerService, ITimeProvider timeProvider,
            IHttpForwarder httpForwarder, HttpService[] httpServices, IPublisher networkSendQueue, RunProjections runProjections)
        {
            _coreQueues = new List<QueuedHandler>();
            _managerInputBus = new InMemoryBus("manager input bus");
            _managerInputQueue = new QueuedHandler(_managerInputBus, "Projections Master");
            while (_coreQueues.Count < _projectionWorkerThreadCount)
            {
                var coreInputBus = new InMemoryBus("bus");
                var coreQueue = new QueuedHandler(
                    coreInputBus, "Projection Core #" + _coreQueues.Count, groupName: "Projection Core");
                var projectionNode = new ProjectionWorkerNode(db, coreQueue, timeProvider, runProjections);
                projectionNode.SetupMessaging(coreInputBus);


                var forwarder = new RequestResponseQueueForwarder(
                    inputQueue: coreQueue, externalRequestQueue: mainQueue);
                // forwarded messages
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadEvent>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadStreamEventsBackward>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadStreamEventsForward>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.ReadAllEventsForward>(forwarder);
                projectionNode.CoreOutput.Subscribe<ClientMessage.WriteEvents>(forwarder);


                if (runProjections >= RunProjections.System)
                {
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.StateReport>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.ResultReport>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.StatisticsReport>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.Started>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.Stopped>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.Faulted>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.Prepared>(_managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>(
                            _managerInputQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<ProjectionManagementMessage.ControlMessage>(_managerInputQueue));

                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<AwakeServiceMessage.SubscribeAwake>(mainQueue));
                    projectionNode.CoreOutput.Subscribe(
                        Forwarder.Create<AwakeServiceMessage.UnsubscribeAwake>(mainQueue));

                }
                projectionNode.CoreOutput.Subscribe<TimerMessage.Schedule>(timerService);


                projectionNode.CoreOutput.Subscribe(Forwarder.Create<Message>(coreQueue)); // forward all

                coreInputBus.Subscribe(new UnwrapEnvelopeHandler());

                _coreQueues.Add(coreQueue);
            }

            _managerInputBus.Subscribe(
            Forwarder.CreateBalancing<FeedReaderMessage.ReadPage>(_coreQueues.Cast<IPublisher>().ToArray()));

            var awakeReaderService = new AwakeService();
            mainBus.Subscribe<StorageMessage.EventCommitted>(awakeReaderService);
            mainBus.Subscribe<StorageMessage.TfEofAtNonCommitRecord>(awakeReaderService);
            mainBus.Subscribe<AwakeServiceMessage.SubscribeAwake>(awakeReaderService);
            mainBus.Subscribe<AwakeServiceMessage.UnsubscribeAwake>(awakeReaderService);


            _projectionManagerNode = ProjectionManagerNode.Create(
                db, _managerInputQueue, httpForwarder, httpServices, networkSendQueue,
                _coreQueues.Cast<IPublisher>().ToArray(), runProjections);
            _projectionManagerNode.SetupMessaging(_managerInputBus);
            {
                var forwarder = new RequestResponseQueueForwarder(
                    inputQueue: _managerInputQueue, externalRequestQueue: mainQueue);
                _projectionManagerNode.Output.Subscribe<ClientMessage.ReadEvent>(forwarder);
                _projectionManagerNode.Output.Subscribe<ClientMessage.ReadStreamEventsBackward>(forwarder);
                _projectionManagerNode.Output.Subscribe<ClientMessage.ReadStreamEventsForward>(forwarder);
                _projectionManagerNode.Output.Subscribe<ClientMessage.WriteEvents>(forwarder);
                _projectionManagerNode.Output.Subscribe(
                    Forwarder.Create<ProjectionManagementMessage.RequestSystemProjections>(mainQueue));
                _projectionManagerNode.Output.Subscribe(Forwarder.Create<Message>(_managerInputQueue));

                _projectionManagerNode.Output.Subscribe<TimerMessage.Schedule>(timerService);

                // self forward all

                mainBus.Subscribe(Forwarder.Create<SystemMessage.StateChangeMessage>(_managerInputQueue));
                _managerInputBus.Subscribe(new UnwrapEnvelopeHandler());
            }
        }

        public void Start()
        {
            if (_managerInputQueue != null) 
                _managerInputQueue.Start();
            foreach (var queue in _coreQueues)
                queue.Start();
        }

        public void Stop()
        {
            if (_managerInputQueue != null) 
                _managerInputQueue.Stop();
            foreach (var queue in _coreQueues)
                queue.Stop();
        }
    }
}
