// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NCrontab;

namespace EventStore.AutoScavenge.Tests;

public static class Schedule {
	public static CrontabSchedule EveryHour => CrontabSchedule.Parse("0 * * * *");
	public static CrontabSchedule EveryDay => CrontabSchedule.Parse("0 0 * * *");
	public static CrontabSchedule EveryWeek => CrontabSchedule.Parse("0 0 * * 0");
}
