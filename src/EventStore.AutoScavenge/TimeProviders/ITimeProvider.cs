// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.AutoScavenge.TimeProviders;

/// <summary>
/// Time abstraction providing the current date.
/// </summary>
public interface ITimeProvider {
	DateTime Now { get; }
}
