using System;
using System.Collections.Generic;
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services.Http;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core
{
    public class ProjectionManagerNode
    {
        private readonly RunProjections _runProjections;
        private readonly ProjectionManager _projectionManager;
        private readonly InMemoryBus _output;
        private readonly TimeoutScheduler[] _timeoutSchedulers;

        private ProjectionManagerNode(
            IPublisher inputQueue,
            IDictionary<Guid, IPublisher> queues,
            RunProjections runProjections,
            TimeoutScheduler[] timeoutSchedulers)
        {
            _runProjections = runProjections;
            _output = new InMemoryBus("ProjectionManagerOutput");
            _timeoutSchedulers = timeoutSchedulers;
            _projectionManager = new ProjectionManager(
                inputQueue,
                _output,
                queues,
                new RealTimeProvider(),
                runProjections,
                _timeoutSchedulers);
        }

        public InMemoryBus Output
        {
            get { return _output; }
        }

        public void SetupMessaging(ISubscriber mainBus)
        {
            mainBus.Subscribe<SystemMessage.StateChangeMessage>(_projectionManager);
            if (_runProjections >= RunProjections.System)
            {
                mainBus.Subscribe<ProjectionManagementMessage.Post>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.UpdateQuery>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.GetQuery>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Delete>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.GetStatistics>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.GetState>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.GetResult>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Disable>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Enable>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Abort>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.SetRunAs>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Reset>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.StartSlaveProjections>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.RegisterSystemProjection>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Internal.CleanupExpired>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Internal.RegularTimeout>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.Internal.Deleted>(_projectionManager);
                mainBus.Subscribe<ProjectionManagementMessage.RegisterSystemProjection>(_projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.Started>(_projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.Stopped>(_projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.Faulted>(_projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.Prepared>(_projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.StateReport>(_projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.ResultReport>(_projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.StatisticsReport>(_projectionManager);
                mainBus.Subscribe<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>(_projectionManager);
                mainBus.Subscribe<PartitionProcessingResultBase>(_projectionManager);
            }
            mainBus.Subscribe<ClientMessage.WriteEventsCompleted>(_projectionManager);
            mainBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_projectionManager);
        }

        public static ProjectionManagerNode Create(
            TFChunkDb db,
            QueuedHandler inputQueue,
            IHttpForwarder httpForwarder,
            HttpService[] httpServices,
            IPublisher networkSendQueue,
            IDictionary<Guid, IPublisher> queues,
            RunProjections runProjections,
            TimeoutScheduler[] timeoutSchedulers)
        {
            var projectionManagerNode = new ProjectionManagerNode(inputQueue, queues, runProjections, timeoutSchedulers);
            var projectionsController = new ProjectionsController(httpForwarder, inputQueue, networkSendQueue);
            foreach (var httpService in httpServices)
            {
                httpService.SetupController(projectionsController);
            }
            return projectionManagerNode;
        }

        public static TimeoutScheduler[] CreateTimeoutSchedulers(int count)
        {
            var timeoutSchedulers = new TimeoutScheduler[count];
            for (var i = 0; i < timeoutSchedulers.Length; i++)
                timeoutSchedulers[i] = new TimeoutScheduler();
            return timeoutSchedulers;
        }
    }
}