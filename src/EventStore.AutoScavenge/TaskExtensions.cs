// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using Serilog;

namespace EventStore.AutoScavenge;

public static class TaskExtensions {
	public static Task WarnOnCompletion(this Task task, string warning) {
		return task.ContinueWith(t => {
			if (t.Exception is null)
				return;
			Log.Warning(t.Exception, warning);
		});
	}
}
