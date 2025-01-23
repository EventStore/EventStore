using System.Linq;
using Dapper;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Duck.Default;

public class DefaultIndex<TStreamId> {
	readonly DuckDb _db;

	public DefaultIndex(DuckDb db, IReadIndex<TStreamId> index) {
		_db = db;
		StreamIndex = new(db);
		CategoryIndex = new(db);
		EventTypeIndex = new(db);
		CategoryIndexReader = new(CategoryIndex, index);
		EventTypeIndexReader = new(EventTypeIndex, index);
		Handler = new(db, this);
		DefaultIndexReader = new(_db, Handler, index);
	}

	public void Init() {
		CategoryIndex.Init();
		EventTypeIndex.Init();
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

	internal readonly CategoryIndexReader<TStreamId> CategoryIndexReader;
	internal readonly EventTypeIndexReader<TStreamId> EventTypeIndexReader;
	internal DefaultIndexReader<TStreamId> DefaultIndexReader;

	internal DefaultIndexHandler<TStreamId> Handler;
}

public record struct SequenceRecord(long Id, long Sequence);
