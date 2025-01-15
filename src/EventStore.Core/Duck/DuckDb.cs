// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.IO;
using System.Threading.Tasks;
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
		Connection.Close();
	}

	public static DuckDBConnection Connection;
}
