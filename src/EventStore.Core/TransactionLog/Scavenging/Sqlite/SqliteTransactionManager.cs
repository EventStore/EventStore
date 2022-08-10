using Microsoft.Data.Sqlite;

namespace EventStore.Core.TransactionLog.Scavenging.Sqlite {
	public class SqliteTransactionManager : TransactionManager<SqliteTransaction> {
		public SqliteTransactionManager(ITransactionFactory<SqliteTransaction> factory,
			IScavengeMap<Unit, ScavengeCheckpoint> storage) : base(factory, storage) {
		}
	}
}
