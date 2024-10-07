// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.DataStructures;

namespace EventStore.Core.LogV3.FASTER;

public static class ObjectPoolExtensions {
	public static Lease<T> Rent<T>(this ObjectPool<T> pool) => new(pool);
}

