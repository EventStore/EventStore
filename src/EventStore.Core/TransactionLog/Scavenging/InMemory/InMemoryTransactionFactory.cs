namespace EventStore.Core.TransactionLog.Scavenging {
	public class InMemoryTransactionFactory : ITransactionFactory<int> {
		public InMemoryTransactionFactory() {
		}

		public int Begin() {
			return 5;
		}

		public void Commit(int transasction) {
		}

		public void Rollback(int transaction) {
		}
	}
}
