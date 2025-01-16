using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Dapper;
using EventStore.Core.Metrics;
using Eventuous.Subscriptions.Context;

namespace EventStore.Core.Duck.Default;

public class EventTypeIndexReader : DuckIndexReader {
	protected override long GetId(string streamName) {
		if (!streamName.StartsWith("$etype-")) {
			throw new InvalidOperationException($"Stream {streamName} is not an event type stream");
		}

		var eventType = streamName[(streamName.IndexOf('-') + 1)..];
		return EventTypeIndex.EventTypes[eventType];
	}

	protected override long GetLastNumber(long id) => EventTypeIndex.GetLastEventNumber(id);

	protected override IEnumerable<IndexedPrepare> GetIndexRecords(long id, long fromEventNumber, long toEventNumber)
		=> EventTypeIndex.GetRecords(id, fromEventNumber, toEventNumber);
}

public static class EventTypeIndex {
	public static Dictionary<long, string> EventTypeIds = new();
	public static Dictionary<string, long> EventTypes = new();
	static readonly Dictionary<long, long> Sequences = new();

	public static void Init() {
		var ids = DuckDb.Connection.Query<ReferenceRecord>("select * from event_type").ToList();
		EventTypeIds = ids.ToDictionary(x => x.id, x => x.name);
		EventTypes = ids.ToDictionary(x => x.name, x => x.id);
		Seq = EventTypeIds.Count > 0 ? EventTypeIds.Keys.Max() : 0;

		foreach (var id in ids) {
			Sequences[id.id] = -1;
		}
		const string query = "select event_type, max(event_type_seq) from idx_all group by event_type";
		var sequences = DuckDb.Connection.Query<(long Id, long Sequence)>(query);
		foreach (var sequence in sequences) {
			Sequences[sequence.Id] = sequence.Sequence;
		}
	}

	public static long GetLastEventNumber(long id) {
		return Sequences[id];
	}

	public static IEnumerable<IndexedPrepare> GetRecords(long id, long fromEventNumber, long toEventNumber) {
		var range = QueryEventType(id, fromEventNumber, toEventNumber);
		var indexPrepares = range.Select(x => new IndexedPrepare(x.event_type_seq, x.stream, id, x.event_number, x.log_position));
		return indexPrepares;
	}

	[MethodImpl(MethodImplOptions.Synchronized)]
	static List<EventTypeRecord> QueryEventType(long eventTypeId, long fromEventNumber, long toEventNumber) {
		const string query = """
		                     select event_type_seq, log_position, event_number, stream
		                     from idx_all where event_type=$et and event_type_seq>=$start and event_type_seq<=$end
		                     """;

		while (true) {
			using var duration = TempIndexMetrics.MeasureIndex("duck_get_et_range");
			var result = DuckDb.QueryWithRetry<EventTypeRecord>(query, new { et = eventTypeId, start = fromEventNumber, end = toEventNumber }).ToList();
			return result;
		}
	}

	public static SequenceRecord Handle(IMessageConsumeContext ctx) {
		if (EventTypes.TryGetValue(ctx.MessageType, out var val)) {
			var next = Sequences[val] + 1;
			Sequences[val] = next;
			return new(val, next);
		}

		var id = ++Seq;
		DuckDb.ExecuteWithRetry(Sql, new { id, name = ctx.MessageType });
		ctx.LogContext.InfoLog?.Log("Stored event type {EventType} with {Id}", ctx.MessageType, id);
		EventTypes[ctx.MessageType] = id;
		EventTypeIds[id] = ctx.MessageType;
		Sequences[id] = 0;
		return new(id, 0);
	}

	static long Seq;
	static readonly string Sql = Default.Sql.AppendIndexSql.Replace("{table}", "event_type");
}
