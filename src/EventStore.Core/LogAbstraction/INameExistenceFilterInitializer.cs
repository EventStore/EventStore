namespace EventStore.Core.LogAbstraction {
	public interface INameExistenceFilterInitializer {
		void Initialize(INameExistenceFilter filter, long truncateToPosition);
	}
}
