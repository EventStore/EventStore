using System.Collections.Generic;
using System.Linq;
using Dapper;
using Eventuous.Subscriptions.Context;

namespace EventStore.Core.Duck.Default;

public static class EventTypeIndex {
	public static Dictionary<long, string> EventTypeIds = new();
	public static Dictionary<string, long> EventTypes = new();
	static Dictionary<long, long> Sequences = new();

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

	public static SequenceRecord Handle(IMessageConsumeContext ctx) {
		if (EventTypes.TryGetValue(ctx.MessageType, out var val)) {
			var next = Sequences[val] + 1;
			Sequences[val] = next;
			return new(val, next);
		}

		var id = ++Seq;
		DuckDb.Connection.Execute(Sql, new { id, name = ctx.MessageType });
		ctx.LogContext.InfoLog?.Log("Stored event type {EventType} with {Id}", ctx.MessageType, id);
		EventTypes[ctx.MessageType] = id;
		EventTypeIds[id] = ctx.MessageType;
		Sequences[id] = 0;
		return new(id, 0);
	}

	static long Seq;
	static readonly string Sql = Default.Sql.AppendIndexSql.Replace("{table}", "event_type");
}
