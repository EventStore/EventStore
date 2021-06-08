using EventStore.Core.Index;

namespace EventStore.Core.LogAbstraction {
	public interface INameExistenceFilterInitializer {
		void SetTableIndex(ITableIndex tableIndex); //qq hmm
		void Initialize(INameExistenceFilter filter);
	}
}
