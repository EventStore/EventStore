namespace EventStore.Core.TransactionLogV2.Chunks {
	public interface ITFChunkScavengerLogManager {
		void Initialise();

		ITFChunkScavengerLog CreateLog();
	}
}
