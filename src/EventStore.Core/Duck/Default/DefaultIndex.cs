using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace EventStore.Core.Duck.Default;

public class DefaultIndex {
	readonly DuckDb _db;

	public DefaultIndex(DuckDb db) {
		_db = db;
		StreamIndex = new(db);
		CategoryIndex = new(db);
		EventTypeIndex = new(db);
		CategoryIndexReader = new(CategoryIndex, StreamIndex, EventTypeIndex);
		EventTypeIndexReader = new(EventTypeIndex, StreamIndex);
	}

	public void Init(DefaultIndexHandler handler) {
		CategoryIndex.Init();
		EventTypeIndex.Init();
		DefaultIndexReader = new(handler, StreamIndex, EventTypeIndex);
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

class DefaultIndexReader(DefaultIndexHandler handler, StreamIndex streamIndex, EventTypeIndex eventTypeIndex) : DuckIndexReader(streamIndex, eventTypeIndex) {
	protected override long GetId(string streamName) => 0;

	protected override long GetLastNumber(long id) => (long)handler.GetLastPosition();

	protected override IEnumerable<IndexedPrepare> GetIndexRecords(long id, long fromEventNumber, long toEventNumber) {
		throw new System.NotImplementedException();
	}
}
