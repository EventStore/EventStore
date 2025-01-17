using System.Collections.Generic;
using System.Linq;
using Dapper;

namespace EventStore.Core.Duck.Default;

public static class DefaultIndex {
	public static void Init(DefaultIndexHandler handler) {
		CategoryIndex.Init();
		EventTypeIndex.Init();
		StreamIndex.Init();
		DefaultIndexReader = new(handler);
	}

	public static ulong? GetLastPosition() {
		const string query = "select max(log_position) from idx_all";
		return DuckDb.Connection.Query<ulong?>(query).FirstOrDefault();
	}

	public static ulong? GetLastSequence() {
		const string query = "select max(seq) from idx_all";
		return DuckDb.Connection.Query<ulong?>(query).FirstOrDefault();
	}

	public static readonly CategoryIndexReader CategoryIndexReader = new();
	public static readonly EventTypeIndexReader EventTypeIndexReader = new();
	public static DefaultIndexReader DefaultIndexReader;
}

public record struct SequenceRecord(long Id, long Sequence);

public class DefaultIndexReader(DefaultIndexHandler handler) : DuckIndexReader {
	protected override long GetId(string streamName) => 0;

	protected override long GetLastNumber(long id) => (long)handler.GetLastPosition();

	protected override IEnumerable<IndexedPrepare> GetIndexRecords(long id, long fromEventNumber, long toEventNumber) {
		throw new System.NotImplementedException();
	}
}
