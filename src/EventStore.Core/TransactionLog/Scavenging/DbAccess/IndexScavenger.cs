// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Index;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;

namespace EventStore.Core.TransactionLog.Scavenging.DbAccess;

public class IndexScavenger : IIndexScavenger {
	private readonly ITableIndex _tableIndex;

	public IndexScavenger(ITableIndex tableIndex) {
		_tableIndex = tableIndex;
	}

	public ValueTask ScavengeIndex(
		long scavengePoint,
		Func<IndexEntry, CancellationToken, ValueTask<bool>> shouldKeep,
		IIndexScavengerLog log,
		CancellationToken cancellationToken) {

		return _tableIndex.Scavenge(shouldKeep, log, cancellationToken);
	}
}
