using System;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Services.Monitoring.Stats;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.DataStructures;

namespace EventStore.Core.Services.TimerService {
	public class ThreadBasedScheduler : IMonitoredQueue, IScheduler, IDisposable {
		public string Name {
			get { return _queueStats.Name; }
		}

		private readonly ConcurrentQueueWrapper<ScheduledTask> _pending = new ConcurrentQueueWrapper<ScheduledTask>();

		private readonly PairingHeap<ScheduledTask> _tasks =
			new PairingHeap<ScheduledTask>((x, y) => x.DueTime < y.DueTime);

		private readonly ITimeProvider _timeProvider;

		private readonly Thread _timerThread;
		private volatile bool _stop;

		private readonly QueueStatsCollector _queueStats;
		private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

		public Task Task {
			get { return _tcs.Task; }
		}

		public ThreadBasedScheduler(ITimeProvider timeProvider, QueueStatsManager queueStatsManager) {
			Ensure.NotNull(timeProvider, "timeProvider");
			_timeProvider = timeProvider;
			_queueStats = queueStatsManager.CreateQueueStatsCollector("Timer");

			_timerThread = new Thread(DoTiming);
			_timerThread.IsBackground = true;
			_timerThread.Name = Name;
			_timerThread.Start();
		}

		public void Stop() {
			Dispose();
		}

		public void Schedule(TimeSpan after, Action<IScheduler, object> callback, object state) {
			_pending.Enqueue(new ScheduledTask(_timeProvider.UtcNow.Add(after), callback, state));
		}

		private void DoTiming() {
			try {
				_queueStats.Start();
				QueueMonitor.Default.Register(this);

				while (!_stop) {
					_queueStats.EnterBusy();
					_queueStats.ProcessingStarted<SchedulePendingTasks>(_pending.Count);

					int pending = 0;
					ScheduledTask task;
					while (_pending.TryDequeue(out task)) {
						_tasks.Add(task);
						pending += 1;
					}

					_queueStats.ProcessingEnded(pending);

					_queueStats.ProcessingStarted<ExecuteScheduledTasks>(_tasks.Count);
					int processed = 0;
					while (_tasks.Count > 0 && _tasks.FindMin().DueTime <= _timeProvider.UtcNow) {
						processed += 1;
						var scheduledTask = _tasks.DeleteMin();
						scheduledTask.Action(this, scheduledTask.State);
					}

					_queueStats.ProcessingEnded(processed);

					if (processed == 0) {
						_queueStats.EnterIdle();

						Thread.Sleep(10);
					}
				}
			} catch (Exception ex) {
				_tcs.TrySetException(ex);
				throw;
			} finally {
				_queueStats.Stop();
				QueueMonitor.Default.Unregister(this);
			}
		}

		public void Dispose() {
			_stop = true;
		}

		public QueueStats GetStatistics() {
			return _queueStats.GetStatistics(_tasks.Count);
		}

		private struct ScheduledTask {
			public readonly DateTime DueTime;
			public readonly Action<IScheduler, object> Action;
			public readonly object State;

			public ScheduledTask(DateTime dueTime, Action<IScheduler, object> action, object state) {
				DueTime = dueTime;
				Action = action;
				State = state;
			}
		}

		private class SchedulePendingTasks {
		}

		private class ExecuteScheduledTasks {
		}
	}
}
