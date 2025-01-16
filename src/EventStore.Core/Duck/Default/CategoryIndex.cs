using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EventStore.Core.Data;
using EventStore.Core.Metrics;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using Eventuous.Subscriptions.Context;
using Serilog;

namespace EventStore.Core.Duck.Default;

static class CategoryIndex {
	static Dictionary<string, long> Categories = new();
	static readonly Dictionary<long, long> CategorySizes = new();

	public static void Init() {
		var ids = DuckDb.Connection.Query<ReferenceRecord>("select * from category").ToList();
		Categories = ids.ToDictionary(x => x.name, x => x.id);
		foreach (var id in ids) {
			CategorySizes[id.id] = -1;
		}

		const string query = "select category, max(category_seq) from idx_all group by category";
		var sequences = DuckDb.Connection.Query<(long Id, long Sequence)>(query);
		foreach (var sequence in sequences) {
			CategorySizes[sequence.Id] = sequence.Sequence;
		}

		Seq = Categories.Count > 0 ? Categories.Values.Max() : 0;
	}

	public static async ValueTask<IReadOnlyList<ResolvedEvent>> GetCategoryEvents<TStreamId>(IIndexReader<TStreamId> index, string streamName, long fromEventNumber, long toEventNumber,
		CancellationToken cancellationToken) {
		var range = QueryCategory(streamName, fromEventNumber, toEventNumber);
		using var reader = index.BorrowReader();
		var readPrepares = range
			.Select(async x => (
					Version: x.category_seq,
					StreamId: x.stream,
					EventType: x.event_type,
					EventNumber: x.event_number,
					Prepare: await ReadPrepare(x.log_position, cancellationToken)
				)
			);
		var prepared = await Task.WhenAll(readPrepares);
		var recordsQuery = prepared
			.Where(x => x.Prepare != null)
			.OrderBy(x => x.Version)
			.ToList();
		var streams = StreamIndex.GetStreams(recordsQuery.Select(x => x.StreamId).Distinct());
		var records = recordsQuery
			.Select(x => (Record: x, StreamName: streams[x.StreamId]))
			.Select(x => ResolvedEvent.ForResolvedLink(
				new(x.Record.EventNumber, x.Record.Prepare, x.StreamName, EventTypeIndex.EventTypeIds[x.Record.EventType]),
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

	[MethodImpl(MethodImplOptions.Synchronized)]
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
			var categoryId = Categories[category];
			var result = DuckDb.QueryWithRetry<CategoryRecord>(query, new { cat = categoryId, start = fromEventNumber, end = toEventNumber }).ToList();
			return result;
		}
	}

	public static long GetCategoryLastEventNumber(string streamName) {
		var categoryId = Categories[GetCategoryName(streamName)];
		return CategorySizes[categoryId];
	}

	static long GetCategoryLastEventNumber(long categoryId) {
		while (true) {
			try {
				return DuckDb.Connection.Query<long>("select max(seq) from idx_all where category=$cat", new { cat = categoryId }).SingleOrDefault();
			} catch (Exception e) {
				Log.Warning("Error while reading index: {Exception}", e.Message);
			}
		}
	}

	static string GetStreamCategory(string streamName) {
		var dashIndex = streamName.IndexOf('-');
		return dashIndex == -1 ? streamName : streamName[..dashIndex];
	}

	static string GetCategoryName(string streamName) {
		var dashIndex = streamName.IndexOf('-');
		return dashIndex == -1 ? throw new InvalidOperationException($"Stream {streamName} is not a category stream") : streamName[(dashIndex + 1)..];
	}

	public static SequenceRecord Handle(IMessageConsumeContext ctx) {
		var categoryName = GetStreamCategory(ctx.Stream.ToString());
		if (Categories.TryGetValue(categoryName, out var val)) {
			var next = CategorySizes[val] + 1;
			CategorySizes[val] = next;
			return new(val, next);
		}

		var id = ++Seq;
		DuckDb.ExecuteWithRetry(CatSql, new { id, name = categoryName });
		ctx.LogContext.InfoLog?.Log("Stored category {Category} with {Id}", categoryName, id);
		Categories[categoryName] = id;
		CategorySizes[id] = 0;
		return new(id, 0);
	}

	static long Seq;
	static readonly string CatSql = Sql.AppendIndexSql.Replace("{table}", "category");
}
