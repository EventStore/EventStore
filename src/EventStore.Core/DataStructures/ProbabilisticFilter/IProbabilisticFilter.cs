namespace EventStore.Core.DataStructures.ProbabilisticFilter {
	public interface IProbabilisticFilter<in TItem> {
		void Add(TItem item);
		bool MayExist(TItem item);
	}
}
