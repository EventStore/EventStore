// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.TransactionLog.Scavenging.InMemory;

public class InMemoryTransactionManager : TransactionManager<int> {
	public InMemoryTransactionManager(InMemoryScavengeMap<Unit, ScavengeCheckpoint> storage)
		: base(new InMemoryTransactionFactory(), storage) {
	}
}