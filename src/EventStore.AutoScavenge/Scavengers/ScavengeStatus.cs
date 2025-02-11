// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.AutoScavenge.Scavengers;

public enum ScavengeStatus {
	Unknown, // couldn't get the status
	InProgress,
	Success, // completed successfuly
	Stopped, // intentionally stopped
	Errored, // completed with an error
	NotRunningUnknown, // not running but don't know if it completed/stopped/errored/interrupted
}
