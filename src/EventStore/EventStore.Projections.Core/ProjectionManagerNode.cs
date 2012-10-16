using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Http;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core
{
    public class ProjectionManagerNode
    {
        private readonly ProjectionManager _projectionManager;
        private readonly InMemoryBus _output;

        private ProjectionManagerNode(IPublisher inputQueue, IPublisher[] queues, ICheckpoint checkpointForStatistics)
        {
            _output = new InMemoryBus("ProjectionManagerOutput");
            _projectionManager = new ProjectionManager(inputQueue, _output, queues, checkpointForStatistics);
        }

        public InMemoryBus Output
        {
            get { return _output; }
        }

        public void SetupMessaging(ISubscriber mainBus)
        {
            mainBus.Subscribe<SystemMessage.SystemInit>(_projectionManager);
            mainBus.Subscribe<SystemMessage.SystemStart>(_projectionManager);
            mainBus.Subscribe<ProjectionManagementMessage.Post>(_projectionManager);
            mainBus.Subscribe<ProjectionManagementMessage.UpdateQuery>(_projectionManager);
            mainBus.Subscribe<ProjectionManagementMessage.GetQuery>(_projectionManager);
            mainBus.Subscribe<ProjectionManagementMessage.Delete>(_projectionManager);
            mainBus.Subscribe<ProjectionManagementMessage.GetStatistics>(_projectionManager);
            mainBus.Subscribe<ProjectionManagementMessage.GetState>(_projectionManager);
            mainBus.Subscribe<ProjectionManagementMessage.Disable>(_projectionManager);
            mainBus.Subscribe<ProjectionManagementMessage.Enable>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.StatusReport.Started>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.StatusReport.Stopped>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.StatusReport.Faulted>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.Management.StateReport>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.Management.StatisticsReport>(_projectionManager);
            mainBus.Subscribe<ClientMessage.WriteEventsCompleted>(_projectionManager);
            mainBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_projectionManager);
        }

        public static ProjectionManagerNode Create(TFChunkDb db, QueuedHandler inputQueue, HttpService httpService, IPublisher[] queues)
        {
            var projectionManagerNode =
                new ProjectionManagerNode(inputQueue, queues, db.Config.WriterCheckpoint);
            httpService.SetupController(new ProjectionsController(inputQueue));

            return projectionManagerNode;
        }
    }
}