using EventStore.Common.Options;
using EventStore.Core.Bus;

namespace EventStore.Projections.Core {
	public class ProjectionsStandardComponents {
		private readonly int _projectionWorkerThreadCount;
		private readonly ProjectionType _runProjections;
		private readonly InMemoryBus _leaderOutputBus;
		private readonly IQueuedHandler _leaderInputQueue;
		private readonly InMemoryBus _leaderMainBus;
		private readonly bool _faultOutOfOrderProjections;

		public ProjectionsStandardComponents(
			int projectionWorkerThreadCount,
			ProjectionType runProjections,
			InMemoryBus leaderOutputBus,
			IQueuedHandler leaderInputQueue,
			InMemoryBus leaderMainBus,
			bool faultOutOfOrderProjections) {
			_projectionWorkerThreadCount = projectionWorkerThreadCount;
			_runProjections = runProjections;
			_leaderOutputBus = leaderOutputBus;
			_leaderInputQueue = leaderInputQueue;
			_leaderMainBus = leaderMainBus;
			_faultOutOfOrderProjections = faultOutOfOrderProjections;
		}

		public int ProjectionWorkerThreadCount {
			get { return _projectionWorkerThreadCount; }
		}

		public ProjectionType RunProjections {
			get { return _runProjections; }
		}

		public InMemoryBus LeaderOutputBus {
			get { return _leaderOutputBus; }
		}

		public IQueuedHandler LeaderInputQueue {
			get { return _leaderInputQueue; }
		}

		public InMemoryBus LeaderMainBus {
			get { return _leaderMainBus; }
		}

		public bool FaultOutOfOrderProjections {
			get { return _faultOutOfOrderProjections; }
		}
	}
}
