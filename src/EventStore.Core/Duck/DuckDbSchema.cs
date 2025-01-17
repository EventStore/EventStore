using System.IO;
using System.Linq;
using System.Reflection;
using DuckDB.NET.Data;
using static EventStore.Core.Duck.DuckDb;

namespace EventStore.Core.Duck;

public static class DuckDbSchema {
	static readonly Assembly Assembly = typeof(DuckDbSchema).Assembly;

	public static void CreateSchema(DuckDBConnection connection) {
		var names = Assembly.GetManifestResourceNames().Where(x => x.EndsWith(".sql")).OrderBy(x => x);
		var transaction = connection.BeginTransaction();

		try {
			foreach (var name in names) {
				using var stream = Assembly.GetManifestResourceStream(name);
				using var reader = new StreamReader(stream!);

				var script = reader.ReadToEnd();

				using var cmd = connection.CreateCommand();
				cmd.CommandText = script;
				cmd.Transaction = transaction;

				cmd.ExecuteNonQuery();
			}
		} catch {
			transaction.Rollback();
			throw;
		}

		transaction.CommitAsync();
	}
}
