// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.DataStructures;

namespace EventStore.Core.LogV3.FASTER;

public readonly struct Lease<T> : IDisposable {
	private readonly ObjectPool<T> _pool;
	public T Reader { get; }

	public Lease(ObjectPool<T> pool) {
		_pool = pool;
		Reader = _pool.Get();
	}
	public void Dispose() {
		_pool.Return(Reader);
	}
}

