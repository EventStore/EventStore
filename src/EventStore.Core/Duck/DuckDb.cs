// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using Dapper;
using DuckDB.NET.Data;
using EventStore.Core.Duck.Default;
using EventStore.Core.TransactionLog.Chunks;
using Serilog;

namespace EventStore.Core.Duck;

public class DuckDb(TFChunkDbConfig dbConfig) {
	static readonly ILogger Log = Serilog.Log.ForContext(typeof(DuckDb));

	public void InitDb() {
		Connection.Open();
		DuckDbSchema.CreateSchema(Connection);
	}

	public void Close() {
		Connection.Execute("checkpoint");
		Connection.Close();
	}

	public DuckDBConnection Connection = new($"Data Source={Path.Combine(dbConfig.Path, "index.db")};");
}

static class DuckDbExtensions {
	public static void ExecuteWithRetry(this DuckDBConnection connection, string sql, object arg) {
		while (true) {
			try {
				connection.Execute(sql, arg);
				return;
			} catch (Exception e) {
				Log.Warning(e, "Error while executing {Sql}", sql);
			}
		}
	}

	public static IEnumerable<T> QueryWithRetry<T>(this DuckDBConnection connection, string sql, object arg = null) {
		while (true) {
			try {
				return connection.Query<T>(sql, arg);
			} catch (Exception e) {
				Log.Warning("Error while executing {Sql}: {Error}", sql, e.Message);
			}
		}
	}

	public static void CloseWithRetry(this DuckDBAppender appender, string type) {
		var i = 5;
		while (i-- >= 0) {
			try {
				appender.Close();
				return;
			} catch (Exception e) {
				Log.Warning(e, "Error while closing appender {Type}: {Error}", type, e.Message);
			}
		}
	}
}
