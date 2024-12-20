// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.TransactionLog.Scavenging.Interfaces;

namespace EventStore.Core.TransactionLog.Scavenging.InMemory;

public class InMemoryTransactionFactory : ITransactionFactory<int> {
	int _transactionNumber;

	public InMemoryTransactionFactory() {
	}

	public int Begin() {
		return _transactionNumber++;
	}

	public void Commit(int transasction) {
	}

	public void Rollback(int transaction) {
		throw new NotImplementedException();
	}
}
