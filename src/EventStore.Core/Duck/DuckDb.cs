// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DuckDB.NET.Data;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Metrics;
using EventStore.Core.Services.Storage.ReaderIndex;
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
		const string query = "select event_number, log_position from idx_all where stream=$stream and event_number>=$start and event_number<=$end";

		while (true) {
			using var duration = TempIndexMetrics.MeasureIndex("duck_get_range");
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

	public static async ValueTask<IReadOnlyList<ResolvedEvent>> GetCategoryEvents<TStreamId>(IIndexReader<TStreamId> reader, TStreamId streamId, string streamName, long fromEventNumber, int maxCount,
		CancellationToken cancellationToken) {
		var range = QueryCategory(streamName, fromEventNumber, fromEventNumber + maxCount - 1);
		var recordsQuery = range
			.ToAsyncEnumerable()
			.SelectAwaitWithCancellation(async (x, ct)
				=> (Version: x.category_seq, StreamName: x.stream_name, EventType: x.event_type, EventNumber: x.event_number, Prepare: await reader.ReadPrepare(streamId, x.log_position, ct))
			)
			.Where(x => x.Prepare != null)
			.OrderByDescending(x => x.Version);
		var records = recordsQuery
			.Reverse()
			.Select(x => ResolvedEvent.ForResolvedLink(
				new(x.EventNumber, x.Prepare, x.StreamName, x.EventType),
				new(
					x.Version,
					x.Prepare.LogPosition,
					x.Prepare.CorrelationId,
					x.Prepare.EventId,
					x.Prepare.TransactionPosition,
					x.Prepare.TransactionOffset,
					x.StreamName,
					x.Version,
					x.Prepare.TimeStamp,
					x.Prepare.Flags,
					"$>",
					[],
					[]
				))
			);
		return await records.ToListAsync(cancellationToken);
	}

	static List<CategoryRecord> QueryCategory(string streamName, long fromEventNumber, long toEventNumber) {
		const string query = """
		                     select category_seq, log_position, event_number, event_type.name as event_type, stream.name as stream_name
		                     from idx_all
		                     inner join stream on idx_all.stream = stream.id
		                     inner join event_type on idx_all.event_type = event_type.id
		                     where category=$cat and category_seq>=$start and category_seq<=$end
		                     """;

		var dashIndex = streamName.IndexOf('-');
		if (dashIndex == -1) {
			throw new InvalidOperationException($"Stream {streamName} is not a category stream");
		}

		var category = streamName[(dashIndex + 1)..];

		while (true) {
			using var duration = TempIndexMetrics.MeasureIndex("duck_get_cat_range");
			try {
				var categoryId = GetCategoryId(category);
				var result = Connection.Query<CategoryRecord>(query, new { cat = categoryId, start = fromEventNumber, end = toEventNumber });
				return result.ToList();
			} catch (Exception e) {
				Log.Warning("Error while reading index: {Exception}", e.Message);
				duration.SetException(e);
			}
		}
	}

	public static IReadOnlyList<IndexEntry> GetCategoryRange(string streamName, long fromEventNumber, long toEventNumber) {
		const string query = "select category_seq, log_position from idx_all where category=$cat and category_seq>=$start and category_seq<=$end";

		var dashIndex = streamName.IndexOf('-');
		if (dashIndex == -1) {
			throw new InvalidOperationException($"Stream {streamName} is not a category stream");
		}

		var category = streamName[(dashIndex + 1)..];

		while (true) {
			using var duration = TempIndexMetrics.MeasureIndex("duck_get_cat_range");
			try {
				var categoryId = GetCategoryId(category);
				var result = Connection.Query<CategoryRecord>(query, new { cat = categoryId, start = fromEventNumber, end = toEventNumber });
				var entries = result.Select(x => new IndexEntry(0, x.category_seq, x.log_position)).ToList();
				return entries;
			} catch (Exception e) {
				Log.Warning("Error while reading index: {Exception}", e.Message);
				duration.SetException(e);
			}
		}
	}

	public static long GetCategoryLastEventNumber(string streamName) {
		var dashIndex = streamName.IndexOf('-');
		if (dashIndex == -1) {
			throw new InvalidOperationException($"Stream {streamName} is not a category stream");
		}

		var category = streamName[(dashIndex + 1)..];
		while (true) {
			try {
				var categoryId = GetCategoryId(category);
				return Connection.Query<long>("select max(seq) from idx_all where category=$cat", new { cat = categoryId }).SingleOrDefault();
			} catch (Exception e) {
				Log.Warning("Error while reading index: {Exception}", e.Message);
			}
		}
	}

	static long GetCategoryId(string category) {
		return Cache.GetOrCreate(category, GetFromDb);

		static long GetFromDb(ICacheEntry arg) {
			const string sql = "select id from category where name=$name";
			arg.SlidingExpiration = TimeSpan.FromDays(7);
			var id = Connection.Query<long>(sql, new { name = arg.Key }).SingleOrDefault();
			Log.Information("Resolved category {Category} to {Id}", arg.Key, id);
			return id;
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
		public long event_number { get; set; }
		public string event_type { get; set; }
		public string stream_name { get; set; }
	}
}

public record struct DuckIndexEntry(long Position, long EventNumber, long Sequence);
