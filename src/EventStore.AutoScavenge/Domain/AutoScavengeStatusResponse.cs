// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NCrontab;

namespace EventStore.AutoScavenge.Domain;

public record AutoScavengeStatusResponse(
	AutoScavengeStatusResponse.Status State,
	CrontabSchedule? Schedule,
	TimeSpan? TimeUntilNextCycle) {

	public enum Status {
		NotConfigured,
		Waiting,
		InProgress,
		Pausing,
		Paused,
	}
}
