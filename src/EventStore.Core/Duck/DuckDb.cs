// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using DuckDB.NET.Data;
using EventStore.Core.Index;
using EventStore.Core.Metrics;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace EventStore.Core.Duck;

public static class DuckDb {
	static readonly ILogger Log = Serilog.Log.ForContext(typeof(DuckDb));
	public static readonly bool UseDuckDb = Environment.GetEnvironmentVariable("ES_USE_DUCKDB") == "1";

	public static void Init() {
		if (!UseDuckDb) return;
		Connection = new("Data Source=./data/file.db");
		Connection.Open();
		Connection.Execute("SET threads TO 10;");
	}

	public static void Close() {
		if (!UseDuckDb) return;
		Connection.Close();
	}

	static DuckDBConnection Connection;

	public static IReadOnlyList<IndexEntry> GetRange(string streamName, ulong streamId, long fromEventNumber, long toEventNumber) {
		const string query = "select event_number, log_position from idx_all where stream=$stream and event_number>=$start and event_number<=$end order by event_number limit $end";

		while (true) {
			using var duration = TempIndexMetrics.MeasureIndex("get_range");
			try {
				var stream = GetStreamId(streamName);
				var result = Connection.Query<IndexRecord>(query, new { stream, start = fromEventNumber, end = toEventNumber });
				return result.Select(x => new IndexEntry(streamId, x.event_number, x.log_position)).ToList();
			} catch (Exception e) {
				// Log.Warning("Error while reading index: {Exception}", e.Message);
				duration.SetException(e);
			}
		}
	}

	public static IReadOnlyList<IndexEntry> GetCategoryRange(string streamName, ulong streamId, long fromEventNumber, long toEventNumber) {
		const string query = "select category_seq, log_position from idx_all where category=$cat and category_seq>=$start and category_seq<=$end";

		var dashIndex = streamName.IndexOf('-');
		if (dashIndex == -1) {
			throw new InvalidOperationException($"Stream {streamName} is not a category stream");
		}

		var category = streamName[(dashIndex + 1)..];

		while (true) {
			using var duration = TempIndexMetrics.MeasureIndex("get_range");
			try {
				var categoryId = GetCategoryId();
				var result = Connection.Query<CategoryRecord>(query, new { cat = categoryId, start = fromEventNumber, end = toEventNumber });
				var entries = result.Select(x => new IndexEntry(streamId, x.category_seq, x.log_position)).ToList();
				return entries;
			} catch (Exception e) {
				Log.Warning("Error while reading index: {Exception}", e.Message);
				duration.SetException(e);
			}
		}

		long GetCategoryId() {
			return Cache.GetOrCreate(category, GetFromDb);

			static long GetFromDb(ICacheEntry arg) {
				const string sql = "select id from category where name=$name";
				arg.SlidingExpiration = TimeSpan.FromDays(7);
				var id = Connection.Query<long>(sql, new { name = arg.Key }).SingleOrDefault();
				Log.Information("Resolved category {Category} to {Id}", arg.Key, id);
				return id;
			}
		}
	}

	static long GetStreamId(string streamName) {
		return Cache.GetOrCreate(streamName, GetFromDb);

		static long GetFromDb(ICacheEntry arg) {
			const string sql = "select id from streams where name=$name";
			arg.SlidingExpiration = TimeSpan.FromMinutes(10);
			return Connection.Query<long>(sql, new { name = arg.Key }).SingleOrDefault();
		}
	}

	static readonly MemoryCache Cache = new(new MemoryCacheOptions());

	class IndexRecord {
		public int event_number { get; set; }
		public long log_position { get; set; }
	}

	class CategoryRecord {
		public int category_seq { get; set; }
		public long log_position { get; set; }
	}
}
