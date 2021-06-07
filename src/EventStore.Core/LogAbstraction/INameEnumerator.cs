using EventStore.Core.Index;

namespace EventStore.Core.LogAbstraction {
	//qq maybe reanme to ...Initializer? Visitor?
	public interface INameEnumerator {
		void SetTableIndex(ITableIndex tableIndex); //qq hmm
		void Initialize(INameExistenceFilter filter);
	}
}
