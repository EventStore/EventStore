namespace EventStore.Core.TransactionLog.Scavenging {
	public class InMemoryTransactionFactory : ITransactionFactory<int> {
		public InMemoryTransactionFactory() {
		}

		public int Begin() {
			// sqlite implementation would open a transaction
			return 5;
		}

		public void Commit(int transasction) {
			// sqlite implementation would commit the transaction
		}

		public void Rollback(int transaction) {
			// sqlite implementation would roll back the transaction
		}
	}
}
