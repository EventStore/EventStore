namespace EventStore.Projections.Core.Services.Processing
{
    public class WorkLoadEstimationStrategy
    {
        private readonly long _maxScheduledSizePerWorker;
        private readonly int _maxUnmeasuredTasksPerWorker;

        public WorkLoadEstimationStrategy(long maxScheduledSizePerWorker, int maxUnmeasuredTasksPerWorker)
        {
            _maxScheduledSizePerWorker = maxScheduledSizePerWorker;
            _maxUnmeasuredTasksPerWorker = maxUnmeasuredTasksPerWorker;
        }

        public bool MayScheduleOn(ParallelProcessingLoadBalancer.WorkerState leastLoadedWorkerState)
        {
            return leastLoadedWorkerState.UnmeasuredTasksScheduled < _maxUnmeasuredTasksPerWorker;
        }

        public long EstimateWorkerLoad(ParallelProcessingLoadBalancer.WorkerState workerState)
        {
            return workerState.UnmeasuredTasksScheduled*10 + workerState.ScheduledSize;
        }
    }
}