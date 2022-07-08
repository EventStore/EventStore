namespace EventStore.Core.TransactionLog.Scavenging.Sqlite {
	public interface IInitializeSqliteBackend {
		void Initialize(SqliteBackend sqlite);
	}
}
