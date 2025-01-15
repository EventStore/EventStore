// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using DuckDB.NET.Data;
using EventStore.Core.Duck.Default;
using EventStore.Core.TransactionLog.Chunks;
using Serilog;

namespace EventStore.Core.Duck;

public static class DuckDb {
	static readonly ILogger Log = Serilog.Log.ForContext(typeof(DuckDb));
	public static readonly bool UseDuckDb = Environment.GetEnvironmentVariable("ES_USE_DUCKDB") == "1";

	public static void Init(TFChunkDbConfig dbConfig) {
		if (!UseDuckDb) return;

		var fileName = Path.Combine(dbConfig.Path, "index.db");
		Connection = new($"Data Source={fileName};");
		Connection.Open();
		DuckDbSchema.CreateSchema();
		DefaultIndex.Init();
	}

	public static void Close() {
		if (!UseDuckDb) return;
		Connection.Execute("checkpoint");
		Connection.Close();
	}

	public static DuckDBConnection Connection;

	public static void ExecuteWithRetry(string sql, object arg) {
		while (true) {
			try {
				Connection.Execute(sql, arg);
				return;
			} catch (Exception e) {
				Log.Warning(e, "Error while executing {Sql}", sql);
			}
		}
	}

	public static IEnumerable<T> QueryWithRetry<T>(string sql, object arg = null) {
		while (true) {
			try {
				return Connection.Query<T>(sql, arg);
			} catch (Exception e) {
				Log.Warning(e, "Error while executing {Sql}", sql);
			}
		}
	}
}
