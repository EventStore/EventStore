// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.Caching;

namespace EventStore.Core.Tests.Caching;

public class EmptyDynamicCache : IDynamicCache {
	public static EmptyDynamicCache Instance { get; } = new();
	public string Name => nameof(EmptyDynamicCache);
	public long Capacity { get; private set; }

	public void SetCapacity(long capacity) {
		Capacity = capacity;
	}

	public void ResetFreedSize() {
	}

	public long Size => 0;
	public long Count => 0;
	public long FreedSize => 0;
}
