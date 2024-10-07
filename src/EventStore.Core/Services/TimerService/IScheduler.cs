// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;

namespace EventStore.Core.Services.TimerService;

public interface IScheduler : IDisposable {
	void Stop();
	void Schedule(TimeSpan after, Action<IScheduler, object> callback, object state);
}
