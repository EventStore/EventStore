using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Dapper;
using DuckDB.NET.Data;
using EventStore.Core.Metrics;
using Eventuous.Subscriptions.Context;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace EventStore.Core.Duck.Default;

static class StreamIndex {
	static long Seq;
	static readonly MemoryCache StreamCache = new(new MemoryCacheOptions());
	static readonly MemoryCacheEntryOptions Options = new() { SlidingExpiration = TimeSpan.FromMinutes(10) };

	public static void Init() {
		const string sql = "select max(id) from streams";
		Seq = DuckDb.Connection.Query<long?>(sql).SingleOrDefault() ?? 0;
	}

	public static long Handle(IMessageConsumeContext ctx) {
		var name = ctx.Stream.ToString();
		if (StreamCache.TryGetValue(name, out var existing)) return (long)existing!;
		var fromDb = GetStreamIdFromDb(name);
		if (fromDb.HasValue) {
			StreamCache.Set(fromDb, ctx.Stream, Options);
			return fromDb.Value;
		}

		var id = ++Seq;
		StreamCache.Set(id, ctx.Stream, Options);
		DuckDb.Connection.Execute(StreamSql, new { id, name = ctx.Stream.ToString() });
		return id;
	}

	public static Dictionary<long, string> GetStreams(IEnumerable<long> ids) {
		var result = new Dictionary<long, string>();
		var uncached = new List<long>();
		foreach (var id in ids) {
			if (StreamCache.TryGetValue(id, out var name)) {
				result.Add(id, (string)name);
				continue;
			}

			uncached.Add(id);
		}

		return uncached.Count == 0 ? result : QueryStreams();

		[MethodImpl(MethodImplOptions.Synchronized)]
		Dictionary<long, string> QueryStreams() {
			while (true) {
				using var duration = TempIndexMetrics.MeasureIndex("duck_get_streams");
				try {
					const string sql = "select * from streams where id in $ids";
					var records = DuckDb.Connection.Query<ReferenceRecord>(sql, new { ids = uncached });
					foreach (var record in records) {
						StreamCache.Set(record.id, record.name, Options);
						result.Add(record.id, record.name);
					}

					return result;
				} catch (Exception e) {
					Log.Warning("Error while querying category events: {Message}", e.Message);
					duration.SetException(e);
				}
			}
		}
	}

	static long GetStreamId(string streamName) {
		return StreamCache.GetOrCreate(streamName, GetFromDb);

		static long GetFromDb(ICacheEntry arg) {
			arg.SlidingExpiration = TimeSpan.FromMinutes(10);
			var id = GetStreamIdFromDb((string)arg.Key);
			return id ?? throw new InvalidOperationException($"Stream {arg.Key} not found");
		}
	}

	static long? GetStreamIdFromDb(string streamName) {
		const string sql = "select id from streams where name=$name";
		return DuckDb.Connection.Query<long?>(sql, new { name = streamName }).SingleOrDefault();
	}

	static readonly string StreamSql = Sql.AppendIndexSql.Replace("{table}", "streams");
	// public override void Handle(IMessageConsumeContext context, DuckDBAppenderRow row) {
	//     var id = CommonHandle(_streamSql, context.Stream);
	//     row.AppendValue(id);
	// }
	//
	// protected override void SetEntryOptions(ICacheEntry entry) => entry.SlidingExpiration = TimeSpan.FromMinutes(10);
}
