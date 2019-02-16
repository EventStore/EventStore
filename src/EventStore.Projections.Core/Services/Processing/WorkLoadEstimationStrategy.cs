namespace EventStore.Projections.Core.Services.Processing {
	public class WorkLoadEstimationStrategy {
		private readonly long _maxScheduledSizePerWorker;
		private readonly int _maxUnmeasuredTasksPerWorker;

		public WorkLoadEstimationStrategy(long maxScheduledSizePerWorker, int maxUnmeasuredTasksPerWorker) {
			_maxScheduledSizePerWorker = maxScheduledSizePerWorker;
			_maxUnmeasuredTasksPerWorker = maxUnmeasuredTasksPerWorker;
		}

		public bool MayScheduleOn(ParallelProcessingLoadBalancer.WorkerState leastLoadedWorkerState) {
			return leastLoadedWorkerState.UnmeasuredTasksScheduled < _maxUnmeasuredTasksPerWorker
			       && leastLoadedWorkerState.ScheduledSize < _maxScheduledSizePerWorker;
		}

		public long EstimateWorkerLoad(ParallelProcessingLoadBalancer.WorkerState workerState) {
			return workerState.UnmeasuredTasksScheduled * 10 + workerState.ScheduledSize;
		}

		public void RemoveTaskLoad(
			ParallelProcessingLoadBalancer.WorkerState workerState,
			ParallelProcessingLoadBalancer.TaskState taskState) {
			if (taskState.Measured) {
				workerState.MeasuredTasksScheduled--;
				workerState.ScheduledSize -= taskState.Size;
			} else {
				workerState.UnmeasuredTasksScheduled--;
			}
		}

		public void AddTaskLoad(
			ParallelProcessingLoadBalancer.WorkerState worker, ParallelProcessingLoadBalancer.TaskState task) {
			if (task.Measured) {
				worker.MeasuredTasksScheduled++;
				worker.ScheduledSize += task.Size;
			} else {
				worker.UnmeasuredTasksScheduled++;
			}
		}
	}
}
