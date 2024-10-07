// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Threading;
using EventStore.Core.Index;

namespace EventStore.Core.TransactionLog.Scavenging;

public class IndexScavenger : IIndexScavenger {
	private readonly ITableIndex _tableIndex;

	public IndexScavenger(ITableIndex tableIndex) {
		_tableIndex = tableIndex;
	}

	public void ScavengeIndex(
		long scavengePoint,
		Func<IndexEntry, bool> shouldKeep,
		IIndexScavengerLog log,
		CancellationToken cancellationToken) {

		_tableIndex.Scavenge(shouldKeep, log, cancellationToken);
	}
}
