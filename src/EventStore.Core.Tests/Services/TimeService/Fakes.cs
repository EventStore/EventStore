// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using EventStore.Core.DataStructures;
using EventStore.Core.Services.TimerService;
using ITimer = EventStore.Core.Services.TimerService.ITimer;

namespace EventStore.Core.Tests.Services.TimeService;

public class TimerBasedScheduler(ITimer timer, ITimeProvider timeProvider) : IScheduler {
	private readonly PairingHeap<ScheduledTask> _tasks = new((x, y) => x.DueTime < y.DueTime);
	private readonly object _queueLock = new();

	public void Stop() {
		Dispose();
	}

	public void Schedule(TimeSpan after, Action<IScheduler, object> callback, object state) {
		lock (_queueLock) {
			_tasks.Add(new ScheduledTask(timeProvider.UtcNow.Add(after), callback, state));
			ResetTimer();
		}
	}

	protected void ProcessOperations() {
		while (_tasks.Count > 0 && _tasks.FindMin().DueTime <= timeProvider.UtcNow) {
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
			timer.FireIn((int)(tuple.DueTime - timeProvider.UtcNow).TotalMilliseconds, OnTimerFired);
		} else {
			timer.FireIn(Timeout.Infinite, OnTimerFired);
		}
	}

	public void Dispose() {
		timer.Dispose();
	}

	private struct ScheduledTask(DateTime dueTime, Action<IScheduler, object> action, object state) {
		public readonly DateTime DueTime = dueTime;
		public readonly Action<IScheduler, object> Action = action;
		public readonly object State = state;
	}
}
