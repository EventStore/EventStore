// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.TimerService;

/// <summary>
/// Timer service uses scheduler that is expected to be already running 
/// when it is passed to constructor and stopped on the disposal. This is done to
/// make sure that we can handle timeouts and callbacks any time
/// (even during system shutdowns and initialization)
/// </summary>
public class TimerService : IDisposable,
	IHandle<SystemMessage.BecomeShutdown>,
	IHandle<TimerMessage.Schedule> {
	private readonly IScheduler _scheduler;

	public TimerService(IScheduler scheduler) {
		_scheduler = scheduler;
	}

	public void Handle(SystemMessage.BecomeShutdown message) {
		_scheduler.Stop();
	}

	public void Handle(TimerMessage.Schedule message) {
		_scheduler.Schedule(
			message.TriggerAfter,
			static (scheduler, state) => OnTimerCallback(scheduler, state),
			message);
	}

	private static void OnTimerCallback(IScheduler scheduler, object state) {
		var msg = (TimerMessage.Schedule)state;
		msg.Reply();
	}

	public void Dispose() {
		_scheduler.Dispose();
	}
}
