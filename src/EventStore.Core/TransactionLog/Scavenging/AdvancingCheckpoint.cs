// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Core.TransactionLog.Scavenging;

// Compares positions to a checkpoint that only advances.
// Only reads the underlying checkpoint when necessary.
public class AdvancingCheckpoint {
	private readonly Func<CancellationToken, ValueTask<long>> _readCheckpoint;
	private long _cachedValue;

	public AdvancingCheckpoint(Func<CancellationToken, ValueTask<long>> readCheckpoint) {
		_readCheckpoint = readCheckpoint;
	}

	public async ValueTask<bool> IsGreaterThanOrEqualTo(long position, CancellationToken ct) {
		if (_cachedValue >= position)
			return true;

		_cachedValue = await _readCheckpoint(ct);

		return _cachedValue >= position;
	}

	public void Reset() {
		_cachedValue = 0;
	}
}
