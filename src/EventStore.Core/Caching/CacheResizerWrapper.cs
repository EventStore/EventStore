using System.Collections.Generic;

namespace EventStore.Core.Caching {
	public class CacheResizerWrapper : ICacheResizer {
		private readonly ICacheResizer _wrapped;

		public CacheResizerWrapper(ICacheResizer wrapped) {
			_wrapped = wrapped;
		}

		public string Name => _wrapped.Name;
		public ResizerUnit Unit => _wrapped.Unit;
		public long ReservedCapacity => _wrapped.ReservedCapacity;
		public int Weight => _wrapped.Weight;
		public long Size => _wrapped.Size;

		public virtual void CalcCapacity(long unreservedCapacity, int totalWeight) =>
			_wrapped.CalcCapacity(unreservedCapacity, totalWeight);

		public IEnumerable<CacheStats> GetStats(string parentKey) =>
			_wrapped.GetStats(parentKey);
	}
}
