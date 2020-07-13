using System;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.DataStructures;

namespace EventStore.Core.Services.TimerService {
	public class TimerBasedScheduler : IDisposable, IScheduler {
		private readonly PairingHeap<ScheduledTask> _tasks =
			new PairingHeap<ScheduledTask>((x, y) => x.DueTime < y.DueTime);

		private readonly ITimeProvider _timeProvider;
		private readonly ITimer _timer;

		private readonly object _queueLock = new object();

		public TimerBasedScheduler(ITimer timer, ITimeProvider timeProvider) {
			Ensure.NotNull(timer, "timer");
			Ensure.NotNull(timeProvider, "timeProvider");

			_timer = timer;
			_timeProvider = timeProvider;
		}

		public void Stop() {
			Dispose();
		}

		public void Schedule(TimeSpan after, Action<IScheduler, object> callback, object state) {
			lock (_queueLock) {
				_tasks.Add(new ScheduledTask(_timeProvider.UtcNow.Add(after), callback, state));
				ResetTimer();
			}
		}

		protected void ProcessOperations() {
			while (_tasks.Count > 0 && _tasks.FindMin().DueTime <= _timeProvider.UtcNow) {
				var scheduledTask = _tasks.DeleteMin();
				scheduledTask.Action(this, scheduledTask.State);
			}
		}

		private void OnTimerFired() {
			lock (_queueLock) {
				ProcessOperations();
				ResetTimer();
			}
		}

		private void ResetTimer() {
			if (_tasks.Count > 0) {
				var tuple = _tasks.FindMin();
				_timer.FireIn((int)(tuple.DueTime - _timeProvider.UtcNow).TotalMilliseconds, OnTimerFired);
			} else {
				_timer.FireIn(Timeout.Infinite, OnTimerFired);
			}
		}

		public void Dispose() {
			_timer.Dispose();
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
	}
}
