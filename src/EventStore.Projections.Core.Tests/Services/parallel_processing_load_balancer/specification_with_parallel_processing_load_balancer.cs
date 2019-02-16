using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.parallel_processing_load_balancer {
	public abstract class specification_with_parallel_processing_load_balancer {
		protected ParallelProcessingLoadBalancer _balancer;

		protected int _workers;
		protected long _maxScheduledSizePerWorker;
		protected int _maxUnmeasuredTasksPerWorker;

		protected abstract void Given();
		protected abstract void When();

		protected virtual ParallelProcessingLoadBalancer GivenBalancer() {
			return new ParallelProcessingLoadBalancer(
				_workers, _maxScheduledSizePerWorker, _maxUnmeasuredTasksPerWorker);
		}

		protected virtual int GivenMaxUnmeasuredTasksPerWorker() {
			return 2;
		}

		protected virtual long GivenMaxScheduledSizePerWorker() {
			return 1000;
		}

		protected virtual int GivenWorkers() {
			return 2;
		}

		[SetUp]
		public void SetUp() {
			_workers = GivenWorkers();
			_maxScheduledSizePerWorker = GivenMaxScheduledSizePerWorker();
			_maxUnmeasuredTasksPerWorker = GivenMaxUnmeasuredTasksPerWorker();
			_balancer = GivenBalancer();
			Given();
			When();
		}
	}
}
