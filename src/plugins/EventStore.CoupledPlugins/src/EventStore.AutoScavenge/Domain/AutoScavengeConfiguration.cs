// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NCrontab;

namespace EventStore.AutoScavenge.Domain;

/// <summary>
/// Auto scavenge configuration transient state.
/// </summary>
public class AutoScavengeConfiguration {

	/// <summary>
	/// Interval between two auto scavenge processes.
	/// </summary>
	public CrontabSchedule? Schedule { get; private set; }

	/// <summary>
	/// If configured at least once, when was the last time the auto scavenge process configuration was updated.
	/// </summary>
	public DateTime? StartingPoint { get; private set; }


	/// <summary>
	/// If the auto scavenge process is configured.
	/// </summary>
	public bool IsConfigured => StartingPoint != null;

	/// <summary>
	/// Deep instance copy, useful for testing purposes
	/// </summary>
	public AutoScavengeConfiguration Clone() {
		return new AutoScavengeConfiguration {
			Schedule = Schedule,
			StartingPoint = StartingPoint,
		};
	}

	/// <summary>
	/// Resets internal state to default values.
	/// </summary>
	public void Clear() {
		Schedule = null;
		StartingPoint = null;
	}

	/// <summary>
	/// Apply an event to the current state.
	/// </summary>
	/// <param name="event"></param>
	public void Apply(IEvent @event) {
		if (@event is not Events.ConfigurationUpdated msg)
			return;

		Handle(msg);
	}

	private void Handle(Events.ConfigurationUpdated msg) {
		Schedule = msg.Schedule;
		StartingPoint = msg.UpdatedAt;
	}
}
