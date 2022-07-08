namespace EventStore.Core.TransactionLog.Scavenging {
	public static class ScavengeStateExtensions {
		public static void SetCheckpoint(
			this IScavengeStateCommon state,
			ScavengeCheckpoint checkpoint) {

			var transaction = state.BeginTransaction();
			try {
				transaction.Commit(checkpoint);
			} catch {
				transaction.Rollback();
				throw;
			}
		}
	}
}
