using System;
using System.Collections.Generic;
using EventStore.Common.Options;
using EventStore.Core.Bus;

namespace EventStore.Projections.Core {
	public class ProjectionsStandardComponents {
		private readonly int _projectionWorkerThreadCount;
		private readonly ProjectionType _runProjections;
		private readonly InMemoryBus _masterOutputBus;
		private readonly IQueuedHandler _masterInputQueue;
		private readonly InMemoryBus _masterMainBus;
		private readonly bool _faultOutOfOrderProjections;

		public ProjectionsStandardComponents(
			int projectionWorkerThreadCount,
			ProjectionType runProjections,
			InMemoryBus masterOutputBus,
			IQueuedHandler masterInputQueue,
			InMemoryBus masterMainBus,
			bool faultOutOfOrderProjections) {
			_projectionWorkerThreadCount = projectionWorkerThreadCount;
			_runProjections = runProjections;
			_masterOutputBus = masterOutputBus;
			_masterInputQueue = masterInputQueue;
			_masterMainBus = masterMainBus;
			_faultOutOfOrderProjections = faultOutOfOrderProjections;
		}

		public int ProjectionWorkerThreadCount {
			get { return _projectionWorkerThreadCount; }
		}

		public ProjectionType RunProjections {
			get { return _runProjections; }
		}

		public InMemoryBus MasterOutputBus {
			get { return _masterOutputBus; }
		}

		public IQueuedHandler MasterInputQueue {
			get { return _masterInputQueue; }
		}

		public InMemoryBus MasterMainBus {
			get { return _masterMainBus; }
		}

		public bool FaultOutOfOrderProjections {
			get { return _faultOutOfOrderProjections; }
		}
	}
}
