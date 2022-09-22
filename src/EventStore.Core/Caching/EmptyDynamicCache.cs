namespace EventStore.Core.Caching {
	public class EmptyDynamicCache : IDynamicCache {
		public EmptyDynamicCache(string name = nameof(EmptyDynamicCache)) {
			Name = name;
		}

		public string Name { get; }
		public long Capacity { get; private set; }

		public void SetCapacity(long capacity) {
			Capacity = capacity;
		}

		public void ResetFreedSize() {
		}

		public long Size => 0;
		public long Count => 0;
		public long FreedSize => 0;
	}
}
