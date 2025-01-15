using System.Linq;
using Dapper;

namespace EventStore.Core.Duck.Default;

public static class DefaultIndex{
	public static void Init() {
		CategoryIndex.Init();
		EventTypeIndex.Init();
		StreamIndex.Init();
	}

	public static ulong? GetLastPosition() {
		const string query = "select max(log_position) from idx_all";

		return DuckDb.Connection.Query<ulong?>(query).FirstOrDefault();
	}
}

public record struct SequenceRecord(long Id, long Sequence);
