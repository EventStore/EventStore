using System;
using System.Collections.Generic;
using EventStore.Common.Options;
using EventStore.Core.Bus;

namespace EventStore.Projections.Core
{
    public class ProjectionsStandardComponents
    {
        private readonly int _projectionWorkerThreadCount;
        private readonly ProjectionType _runProjections;
        private readonly InMemoryBus _masterOutputBus;
        private readonly QueuedHandler _masterInputQueue;
        private readonly InMemoryBus _masterMainBus;

        public ProjectionsStandardComponents(
            int projectionWorkerThreadCount,
            ProjectionType runProjections,
            InMemoryBus masterOutputBus,
            QueuedHandler masterInputQueue,
            InMemoryBus masterMainBus)
        {
            _projectionWorkerThreadCount = projectionWorkerThreadCount;
            _runProjections = runProjections;
            _masterOutputBus = masterOutputBus;
            _masterInputQueue = masterInputQueue;
            _masterMainBus = masterMainBus;
        }

        public int ProjectionWorkerThreadCount
        {
            get { return _projectionWorkerThreadCount; }
        }

        public ProjectionType RunProjections
        {
            get { return _runProjections; }
        }

        public InMemoryBus MasterOutputBus
        {
            get { return _masterOutputBus; }
        }

        public QueuedHandler MasterInputQueue
        {
            get { return _masterInputQueue; }
        }

        public InMemoryBus MasterMainBus
        {
            get { return _masterMainBus; }
        }
    }
}
