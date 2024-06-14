using System;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.DataStructures;
using EventStore.Core.Time;
using EventStore.Core.Services.Monitoring.Stats;
using System.Threading.Tasks;
using EventStore.Core.Metrics;
using Serilog;

namespace EventStore.Core.Services.TimerService {
	public sealed class ThreadBasedScheduler : IMonitoredQueue, IScheduler {
		private static readonly ILogger Log = Serilog.Log.ForContext<ThreadBasedScheduler>();
		public string Name {
			get { return _queueStats.Name; }
		}

		private readonly ConcurrentQueueWrapper<ScheduledTask> _pending = new ConcurrentQueueWrapper<ScheduledTask>();
		private readonly ManualResetEventSlim _pendingEvent = new ManualResetEventSlim(false);

		private readonly PairingHeap<ScheduledTask> _tasks =
			new PairingHeap<ScheduledTask>((x, y) => x.DueTime < y.DueTime);

		private readonly IClock _timeProvider;

		private volatile bool _stop;

		private readonly QueueStatsCollector _queueStats;
		private readonly QueueTracker _tracker;
		private readonly TaskCompletionSource<object> _tcs = new TaskCompletionSource<object>();

		public Task Task {
			get { return _tcs.Task; }
		}

		public ThreadBasedScheduler(
			QueueStatsManager queueStatsManager,
			QueueTrackers trackers,
			IClock timeProvider = null) {
			_timeProvider = timeProvider ?? Clock.Instance;
			_queueStats = queueStatsManager.CreateQueueStatsCollector("Timer");
			_tracker = trackers.GetTrackerForQueue("Timer");

			new Thread(DoTiming) {
				IsBackground = true,
				Name = Name
			}.Start();
		}

		public void Stop() {
			Dispose();
		}

		public void Schedule(TimeSpan after, Action<IScheduler, object> callback, object state) {
			_pending.Enqueue(new ScheduledTask(_timeProvider.Now.Add(after), callback, state));
			_pendingEvent.Set();
		}

		private void DoTiming() {

			_queueStats.Start();
			QueueMonitor.Default.Register(this);

			while (!_stop) {
				try {
					_queueStats.EnterBusy();
					_queueStats.ProcessingStarted<SchedulePendingTasks>(_pending.Count);

					int pending = 0;
					while (_pending.TryDequeue(out var task)) {
						_tasks.Add(task);
						pending += 1;
					}

					_queueStats.ProcessingEnded(pending);

					_queueStats.ProcessingStarted<ExecuteScheduledTasks>(_tasks.Count);
					int processed = 0;
					while (_tasks.Count > 0 && _tasks.FindMin().DueTime <= _timeProvider.Now) {
						processed += 1;
						var scheduledTask = _tasks.DeleteMin();
						var now = _tracker.RecordMessageDequeued(enqueuedAt: scheduledTask.DueTime);
						scheduledTask.Action(this, scheduledTask.State);
						var label = (scheduledTask.State as TimerMessage.Schedule)?.ReplyMessage.Label;
						_tracker.RecordMessageProcessed(now, label ?? "");
					}

					_queueStats.ProcessingEnded(processed);

					if (processed == 0) {
						var timeout = TimeSpan.FromSeconds(5);
						_pendingEvent.Reset();

						if (_tasks.Count > 0) {
							var timeLeftBeforeNextTask = _tasks.FindMin().DueTime.ElapsedTimeSince(_timeProvider.Now);

							if (timeLeftBeforeNextTask <= TimeSpan.Zero)
								// we have already reached the due time of the next task, so we process it immediately
								continue;

							if (timeLeftBeforeNextTask < timeout)
								// the next task is less than the default timeout (5 seconds) from now, so we want to
								// wake up just on time for the next task (if no new and earlier task is scheduled)
								timeout = timeLeftBeforeNextTask;
						}

						_queueStats.EnterIdle();
						_pendingEvent.Wait(timeout);
					}

				} catch (Exception ex) {
					Log.Error(ex, "Error executing scheduled task");
					_tcs.TrySetException(ex);
				}
			}

			_queueStats.Stop();
			QueueMonitor.Default.Unregister(this);
			_pendingEvent.Dispose();
		}

		public void Dispose() {
			_stop = true;
		}

		public QueueStats GetStatistics() {
			return _queueStats.GetStatistics(_tasks.Count);
		}

		private struct ScheduledTask {
			public readonly Instant DueTime;
			public readonly Action<IScheduler, object> Action;
			public readonly object State;

			public ScheduledTask(Instant dueTime, Action<IScheduler, object> action, object state) {
				DueTime = dueTime;
				Action = action;
				State = state;
			}
		}

		private class SchedulePendingTasks;

		private class ExecuteScheduledTasks;
	}
}
