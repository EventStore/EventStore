using System;
using System.Collections.Generic;

namespace EventStore.Projections.Core.Services.Processing {
	public class ParallelProcessingLoadBalancer {
		public class WorkerState {
			public readonly int Worker;
			public int UnmeasuredTasksScheduled;
			public int MeasuredTasksScheduled;
			public long ScheduledSize;

			public WorkerState(int worker) {
				Worker = worker;
			}
		}

		public class TaskState {
			internal int Worker;
			public long Size;
			internal readonly Action<int> Scheduled;
			public bool Measured;

			public TaskState(Action<int> scheduled) {
				Worker = -1;
				Scheduled = scheduled;
			}
		}

		private readonly WorkerState[] _workerState;
		private readonly Dictionary<object, TaskState> _tasks = new Dictionary<object, TaskState>();
		private readonly Queue<TaskState> _pendingTasks = new Queue<TaskState>();
		private readonly WorkLoadEstimationStrategy _workLoadEstimationStrategy;

		public ParallelProcessingLoadBalancer(
			int workers, long maxScheduledSizePerWorker, int maxUnmeasuredTasksPerWorker) {
			if (workers <= 0) throw new ArgumentException("At least one worker required", "workers");
			if (maxScheduledSizePerWorker <= 0) throw new ArgumentException("maxScheduledSizePerWorker <= 0");
			if (maxUnmeasuredTasksPerWorker <= 0) throw new ArgumentException("maxUnmeasuredTasksPerWorker <= 0");

			_workLoadEstimationStrategy = new WorkLoadEstimationStrategy(
				maxScheduledSizePerWorker, maxUnmeasuredTasksPerWorker);
			_workerState = new WorkerState[workers];
			for (int index = 0; index < _workerState.Length; index++)
				_workerState[index] = new WorkerState(index);
		}

		public void AccountMeasured(object task, long size) {
			var taskState = _tasks[task];
			var workerState = _workerState[taskState.Worker];
			_workLoadEstimationStrategy.RemoveTaskLoad(workerState, taskState);
			taskState.Size = size;
			taskState.Measured = true;
			_workLoadEstimationStrategy.AddTaskLoad(workerState, taskState);
			Schedule();
		}

		public void AccountCompleted(object task) {
			var taskState = _tasks[task];
			var workerState = _workerState[taskState.Worker];
			_workLoadEstimationStrategy.RemoveTaskLoad(workerState, taskState);
			Schedule();
		}

		public void ScheduleTask<T>(T task, Action<T, int> scheduled) {
			var index = FindLeastLoaded();
			var leastLoadedWorkerState = _workerState[index];
			var taskState = new TaskState(worker => scheduled(task, worker));
			_tasks.Add(task, taskState);
			if (_workLoadEstimationStrategy.MayScheduleOn(leastLoadedWorkerState)) {
				ScheduleOn(index, taskState);
			} else {
				_pendingTasks.Enqueue(taskState);
			}
		}

		private void Schedule() {
			while (_pendingTasks.Count > 0) {
				var leastLoadedWorker = FindLeastLoaded();
				if (_workLoadEstimationStrategy.MayScheduleOn(_workerState[leastLoadedWorker])) {
					var task = _pendingTasks.Dequeue();
					ScheduleOn(leastLoadedWorker, task);
				} else
					break;
			}
		}

		private void ScheduleOn(int index, TaskState taskState) {
			var worker = _workerState[index];
			taskState.Worker = index;
			_workLoadEstimationStrategy.AddTaskLoad(worker, taskState);
			taskState.Scheduled(index);
		}

		private int FindLeastLoaded() {
			var bestIndex = -1;
			var best = long.MaxValue;
			for (var i = 0; i < _workerState.Length; i++) {
				var current = _workLoadEstimationStrategy.EstimateWorkerLoad(_workerState[i]);
				if (current < best) {
					best = current;
					bestIndex = i;
				}
			}

			return bestIndex;
		}
	}
}
