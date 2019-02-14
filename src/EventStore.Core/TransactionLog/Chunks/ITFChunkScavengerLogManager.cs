namespace EventStore.Core.TransactionLog.Chunks {
	public interface ITFChunkScavengerLogManager {
		void Initialise();

		ITFChunkScavengerLog CreateLog();
	}
}
