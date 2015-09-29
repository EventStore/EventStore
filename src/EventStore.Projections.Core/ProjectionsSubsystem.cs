using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Options;
using EventStore.Core;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.AwakeReaderService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core
{
    public sealed class ProjectionsSubsystem : ISubsystem, IHandle<CoreProjectionStatusMessage.Stopped>
    {
        private readonly int _projectionWorkerThreadCount;
        private readonly ProjectionType _runProjections;
        private readonly bool _startStandardProjections;
        public const int VERSION = 3;

        private QueuedHandler _masterInputQueue;
        private InMemoryBus _masterMainBus;
        private InMemoryBus _masterOutputBus;
        private IDictionary<Guid, QueuedHandler> _coreQueues;
        private Dictionary<Guid, IPublisher> _queueMap;

        public ProjectionsSubsystem(int projectionWorkerThreadCount, ProjectionType runProjections, bool startStandardProjections)
        {
            if (runProjections <= ProjectionType.System)
                _projectionWorkerThreadCount = 1;
            else
                _projectionWorkerThreadCount = projectionWorkerThreadCount;

            _runProjections = runProjections;
            _startStandardProjections = startStandardProjections;
        }

        public void Register(StandardComponents standardComponents)
        {
            _masterMainBus = new InMemoryBus("manager input bus");
            _masterInputQueue = new QueuedHandler(_masterMainBus, "Projections Master");
            _masterOutputBus = new InMemoryBus("ProjectionManagerAndCoreCoordinatorOutput");

            var projectionsStandardComponents = new ProjectionsStandardComponents(
                _projectionWorkerThreadCount,
                _runProjections,
                _masterOutputBus,
                _masterInputQueue,
                _masterMainBus);

            CreateAwakerService(standardComponents);
            _coreQueues = ProjectionCoreWorkersNode.CreateCoreWorkers(standardComponents, projectionsStandardComponents);
            _queueMap = _coreQueues.ToDictionary(v => v.Key, v => (IPublisher)v.Value);

            ProjectionManagerNode.CreateManagerService(standardComponents, projectionsStandardComponents, _queueMap);
            projectionsStandardComponents.MasterMainBus.Subscribe<CoreProjectionStatusMessage.Stopped>(this);
        }

        private static void CreateAwakerService(StandardComponents standardComponents)
        {
            var awakeReaderService = new AwakeService();
            standardComponents.MainBus.Subscribe<StorageMessage.EventCommitted>(awakeReaderService);
            standardComponents.MainBus.Subscribe<StorageMessage.TfEofAtNonCommitRecord>(awakeReaderService);
            standardComponents.MainBus.Subscribe<AwakeServiceMessage.SubscribeAwake>(awakeReaderService);
            standardComponents.MainBus.Subscribe<AwakeServiceMessage.UnsubscribeAwake>(awakeReaderService);
        }


        public void Start()
        {
            if (_masterInputQueue != null)
                _masterInputQueue.Start();
            foreach (var queue in _coreQueues)
                queue.Value.Start();
        }

        public void Stop()
        {
            if (_masterInputQueue != null)
                _masterInputQueue.Stop();
            foreach (var queue in _coreQueues)
                queue.Value.Stop();
        }

        private List<string> _standardProjections = new List<string> { "$by_category", "$stream_by_category", "$streams", "$by_event_type" };
        public void Handle(CoreProjectionStatusMessage.Stopped message)
        {
            if (_startStandardProjections)
            {
                if (_standardProjections.Contains(message.Name))
                {
                    _standardProjections.Remove(message.Name);
                    var envelope = new NoopEnvelope();
                    _masterMainBus.Publish(new ProjectionManagementMessage.Command.Enable(envelope, message.Name, ProjectionManagementMessage.RunAs.System));
                }
            }
        }
    }
}
