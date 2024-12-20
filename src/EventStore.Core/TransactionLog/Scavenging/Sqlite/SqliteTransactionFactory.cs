// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using EventStore.Core.TransactionLog.Scavenging.Interfaces;
using Microsoft.Data.Sqlite;

namespace EventStore.Core.TransactionLog.Scavenging.Sqlite;

public class SqliteTransactionFactory : IInitializeSqliteBackend, ITransactionFactory<SqliteTransaction> {
	private SqliteBackend _sqliteBackend;

	public void Initialize(SqliteBackend sqlite) {
		_sqliteBackend = sqlite;
	}

	public SqliteTransaction Begin() {
		return _sqliteBackend.BeginTransaction();
	}

	public void Rollback(SqliteTransaction transaction) {
		transaction.Rollback();
		transaction.Dispose();
		_sqliteBackend.ClearTransaction();
	}

	public void Commit(SqliteTransaction transaction) {
		transaction.Commit();
		transaction.Dispose();
		_sqliteBackend.ClearTransaction();
	}
}
