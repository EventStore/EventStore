using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Dapper;
using EventStore.Core.Metrics;

namespace EventStore.Core.Duck.Default;

public class DefaultIndex {
	readonly DuckDb _db;
	public DefaultIndexHandler Handler { get; }

	public DefaultIndex(DuckDb db) {
		_db = db;
		StreamIndex = new(db);
		CategoryIndex = new(db);
		EventTypeIndex = new(db);
		CategoryIndexReader = new(CategoryIndex, StreamIndex, EventTypeIndex);
		EventTypeIndexReader = new(EventTypeIndex, StreamIndex);
		Handler = new(db, this);
	}

	public void Init() {
		CategoryIndex.Init();
		EventTypeIndex.Init();
		DefaultIndexReader = new(_db, Handler, StreamIndex, EventTypeIndex);
	}

	public ulong? GetLastPosition() {
		const string query = "select max(log_position) from idx_all";
		return _db.Connection.Query<ulong?>(query).FirstOrDefault();
	}

	public ulong? GetLastSequence() {
		const string query = "select max(seq) from idx_all";
		return _db.Connection.Query<ulong?>(query).FirstOrDefault();
	}

	internal StreamIndex StreamIndex;
	internal CategoryIndex CategoryIndex;
	internal EventTypeIndex EventTypeIndex;

	internal readonly CategoryIndexReader CategoryIndexReader;
	internal readonly EventTypeIndexReader EventTypeIndexReader;
	internal DefaultIndexReader DefaultIndexReader;
}

public record struct SequenceRecord(long Id, long Sequence);

class DefaultIndexReader(DuckDb db, DefaultIndexHandler handler, StreamIndex streamIndex, EventTypeIndex eventTypeIndex)
	: DuckIndexReader(streamIndex, eventTypeIndex) {
	protected override long GetId(string streamName) => 0;

	protected override long GetLastNumber(long id) => (long)handler.GetLastPosition();

	protected override IEnumerable<IndexedPrepare> GetIndexRecords(long _, long fromEventNumber, long toEventNumber) {
		var range = QueryAll(fromEventNumber, toEventNumber);
		var indexPrepares = range.Select(x => new IndexedPrepare(x.seq, x.stream, x.event_type, x.event_number, x.log_position));
		return indexPrepares;
	}

	[MethodImpl(MethodImplOptions.Synchronized)]
	List<AllRecord> QueryAll(long fromEventNumber, long toEventNumber) {
		const string query = "select seq, log_position, event_number, event_type, stream from idx_all where seq>=$start and seq<=$end";

		using var duration = TempIndexMetrics.MeasureIndex("duck_get_all_range");
		var result = db.Connection.QueryWithRetry<AllRecord>(query, new { start = fromEventNumber, end = toEventNumber }).ToList();
		return result;
	}
}
