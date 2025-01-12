// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using DuckDB.NET.Data;
using EventStore.Core.Index;
using EventStore.Core.Metrics;

namespace EventStore.Core.Duck;

public static class DuckDb {
	public static void Init() {
		Connection = new("Data Source=./data/file.db");
		Connection.Open();
	}

	public static void Close() {
		Connection.Close();
	}

	static DuckDBConnection Connection;

	public static IReadOnlyList<IndexEntry> GetRange(string streamName, ulong streamId, long fromEventNumber, int maxCount) {
		using var _ = TempIndexMetrics.MeasureIndex("get_range");
		const string query =
			"select event_number, log_position from idx_all inner join streams on idx_all.stream=streams.id where streams.name=$stream and event_number>=$start order by event_number limit $count";

		while (true) {
			try {
				var result = Connection.Query<IndexRecord>(query, new { stream = streamName, start = fromEventNumber, count = maxCount });
				return result.Select(x => new IndexEntry(streamId, x.event_number, x.log_position)).ToList();
			} catch (Exception e) {
				Console.WriteLine(e);
			}
		}
	}

	class IndexRecord {
		public int event_number { get; set; }
		public long log_position { get; set; }
	}
}
