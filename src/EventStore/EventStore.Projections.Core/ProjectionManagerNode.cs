using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Transport.Http;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Http;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core
{
    public class ProjectionManagerNode
    {
        private readonly ProjectionManager _projectionManager;

        private ProjectionManagerNode(ProjectionManager projectionManager)
        {
            _projectionManager = projectionManager;
        }

        public void SetupMessaging(IBus mainBus)
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
            mainBus.Subscribe<ProjectionMessage.Projections.Started>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.Stopped>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.Faulted>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.Management.StateReport>(_projectionManager);
            mainBus.Subscribe<ProjectionMessage.Projections.Management.StatisticsReport>(_projectionManager);
            mainBus.Subscribe<ClientMessage.WriteEventsCompleted>(_projectionManager);
            mainBus.Subscribe<ClientMessage.ReadEventsBackwardsCompleted>(_projectionManager);
        }

        public static ProjectionManagerNode Create(TFChunkDb db, IPublisher publisher, QueuedHandler mainQueue, HttpService httpService, IPublisher[] queues)
        {
            var projectionManagerNode =
                new ProjectionManagerNode(new ProjectionManager(mainQueue, publisher, queues, db.Config.WriterCheckpoint));
            httpService.SetupController(new ProjectionsController(mainQueue));

            return projectionManagerNode;
        }
    }
}