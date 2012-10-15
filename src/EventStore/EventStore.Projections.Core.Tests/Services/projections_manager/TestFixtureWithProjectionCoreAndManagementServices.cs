using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Tests.Bus.QueuedHandler.Helpers;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public abstract class TestFixtureWithProjectionCoreAndManagementServices: TestFixtureWithExistingEvents
    {
        protected ProjectionManager _manager;
        private ProjectionCoreService _coreService;

        [SetUp]
        public void setup()
        {
            //TODO: this became a n integration test - proper ProjectionCoreService and ProjectionManager testing is required instead
            _bus.Subscribe(_consumer);

            _manager = new ProjectionManager(_bus, new IPublisher[] { _bus }, checkpointForStatistics: null);
            _coreService = new ProjectionCoreService(_bus, 10, new InMemoryCheckpoint(1000));
            _bus.Subscribe<ProjectionMessage.Projections.Stopped>(_manager);
            _bus.Subscribe<ProjectionMessage.Projections.Management.StateReport>(_manager);
            _bus.Subscribe<ProjectionMessage.Projections.Management.StatisticsReport>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);

            _bus.Subscribe<ProjectionMessage.CoreService.Management.Create>(_coreService);
            _bus.Subscribe<ProjectionMessage.CoreService.Management.Dispose>(_coreService);
            _bus.Subscribe<ProjectionMessage.Projections.Management.Start>(_coreService);
            _bus.Subscribe<ProjectionMessage.Projections.Management.Stop>(_coreService);
            _bus.Subscribe<ProjectionMessage.Projections.Management.GetState>(_coreService);
            _bus.Subscribe<ProjectionMessage.Projections.Management.GetStatistics>(_coreService);


            When();
        }

        protected abstract void When();
    }
}