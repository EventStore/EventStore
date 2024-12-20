// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.TransactionLog.Scavenging.Interfaces;
using Microsoft.Data.Sqlite;

namespace EventStore.Core.TransactionLog.Scavenging.Sqlite;

public class SqliteTransactionManager : TransactionManager<SqliteTransaction> {
	public SqliteTransactionManager(ITransactionFactory<SqliteTransaction> factory,
		IScavengeMap<Unit, ScavengeCheckpoint> storage) : base(factory, storage) {
	}
}
