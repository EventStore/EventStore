// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DuckDB.NET.Data;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Metrics;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using Microsoft.Diagnostics.Tracing.Parsers.FrameworkEventSource;
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
		Connection.Execute("SET threads = 10;");
		Connection.Execute("SET memory_limit = '8GB';");

		Categories = Connection.Query<ReferenceRecord>("select * from category").ToDictionary(x => x.name, x => x.id);
		EventTypes = Connection.Query<ReferenceRecord>("select * from event_type").ToDictionary(x => x.id, x => x.name);
		foreach (var categoryId in Categories.Values) {
			CategorySizes[categoryId] = GetCategoryLastEventNumber(categoryId);
		}
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

	public static async ValueTask<IReadOnlyList<ResolvedEvent>> GetCategoryEvents<TStreamId>(IIndexReader<TStreamId> index, string streamName, long fromEventNumber, int maxCount,
		CancellationToken cancellationToken) {
		var range = QueryCategory(streamName, fromEventNumber, fromEventNumber + maxCount - 1);
		using var reader = index.BorrowReader();
		var recordsQuery = await range
			.ToAsyncEnumerable()
			.SelectAwaitWithCancellation(async (x, ct) => (
					Version: x.category_seq,
					StreamId: x.stream,
					EventType: x.event_type,
					EventNumber: x.event_number,
					Prepare: await ReadPrepare(x.log_position, ct)
				)
			)
			.Where(x => x.Prepare != null)
			.OrderBy(x => x.Version)
			.ToListAsync(cancellationToken: cancellationToken);
		var streams = GetStreams(recordsQuery.Select(x => x.StreamId).Distinct());
		var records = recordsQuery
			.Select(x => (Record: x, StreamName: streams[x.StreamId]))
			.Select(x => ResolvedEvent.ForResolvedLink(
				new(x.Record.EventNumber, x.Record.Prepare, x.StreamName, EventTypes[x.Record.EventType]),
				new(
					x.Record.Version,
					x.Record.Prepare.LogPosition,
					x.Record.Prepare.CorrelationId,
					x.Record.Prepare.EventId,
					x.Record.Prepare.TransactionPosition,
					x.Record.Prepare.TransactionOffset,
					x.StreamName,
					x.Record.Version,
					x.Record.Prepare.TimeStamp,
					x.Record.Prepare.Flags,
					"$>",
					Encoding.UTF8.GetBytes($"{x.Record.EventNumber}@{x.StreamName}"),
					[]
				))
			);
		var result = records.ToList();
		return result;

		async ValueTask<IPrepareLogRecord<TStreamId>> ReadPrepare(long logPosition, CancellationToken ct) {
			var r = await reader.TryReadAt(logPosition, couldBeScavenged: true, ct);
			if (!r.Success)
				return null;

			if (r.LogRecord.RecordType is not LogRecordType.Prepare
			    and not LogRecordType.Stream
			    and not LogRecordType.EventType)
				throw new($"Incorrect type of log record {r.LogRecord.RecordType}, expected Prepare record.");
			return (IPrepareLogRecord<TStreamId>)r.LogRecord;
		}
	}

	static List<CategoryRecord> QueryCategory(string streamName, long fromEventNumber, long toEventNumber) {
		const string query = """
		                     select category_seq, log_position, event_number, event_type, stream
		                     from idx_all where category=$cat and category_seq>=$start and category_seq<=$end
		                     """;

		var dashIndex = streamName.IndexOf('-');
		if (dashIndex == -1) {
			throw new InvalidOperationException($"Stream {streamName} is not a category stream");
		}

		var category = streamName[(dashIndex + 1)..];

		while (true) {
			using var duration = TempIndexMetrics.MeasureIndex("duck_get_cat_range");
			try {
				var categoryId = Categories[category];
				var result = Connection.Query<CategoryRecord>(query, new { cat = categoryId, start = fromEventNumber, end = toEventNumber }).ToList();
				return result;
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
				var categoryId = Categories[category];
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
		var categoryId = Categories[category];
		return CategorySizes[categoryId];
	}

	public static long GetCategoryLastEventNumber(long categoryId) {
		while (true) {
			try {
				return Connection.Query<long>("select max(seq) from idx_all where category=$cat", new { cat = categoryId }).SingleOrDefault();
			} catch (Exception e) {
				Log.Warning("Error while reading index: {Exception}", e.Message);
			}
		}
	}

	static long GetStreamId(string streamName) {
		return StreamCache.GetOrCreate(streamName, GetFromDb);

		static long GetFromDb(ICacheEntry arg) {
			const string sql = "select id from streams where name=$name";
			arg.SlidingExpiration = TimeSpan.FromMinutes(10);
			return Connection.Query<long>(sql, new { name = arg.Key }).SingleOrDefault();
		}
	}

	static readonly MemoryCacheEntryOptions Options = new() { SlidingExpiration = TimeSpan.FromMinutes(10) };

	static Dictionary<long, string> GetStreams(IEnumerable<long> ids) {
		using var duration = TempIndexMetrics.MeasureIndex("duck_get_streams");
		var result = new Dictionary<long, string>();
		var uncached = new List<long>();
		foreach (var id in ids) {
			if (StreamCache.TryGetValue(id, out var name)) {
				result.Add(id, (string)name);
				continue;
			}

			uncached.Add(id);
		}

		if (uncached.Count == 0) return result;
		const string sql = "select * from streams where id in $ids";
		var records = Connection.Query<ReferenceRecord>(sql, new { ids = uncached });
		foreach (var record in records) {
			StreamCache.Set(record.id, record.name, Options);
			result.Add(record.id, record.name);
		}

		return result;
	}

	static readonly MemoryCache StreamCache = new(new MemoryCacheOptions());
	static Dictionary<long, string> EventTypes = new();
	static Dictionary<string, long> Categories = new();

	class IndexRecord {
		public int event_number { get; set; }
		public long log_position { get; set; }
	}

	class CategoryRecord {
		public int category_seq { get; set; }
		public long log_position { get; set; }
		public long event_number { get; set; }
		public long event_type { get; set; }
		public long stream { get; set; }
	}

	class ReferenceRecord {
		public long id { get; set; }
		public string name { get; set; }
	}

	static Dictionary<long, long> CategorySizes = new();
}

public record struct DuckIndexEntry(long Position, long EventNumber, long Sequence);
