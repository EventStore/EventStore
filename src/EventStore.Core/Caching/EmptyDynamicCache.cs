namespace EventStore.Core.Caching {
	public class EmptyDynamicCache : IDynamicCache {
		public EmptyDynamicCache(string name = "Empty") {
			Name = name;
		}

		//qq get rid of instance, this class isn't immutable
		public static EmptyDynamicCache Instance { get; } = new();
		public string Name { get; }
		public long Capacity { get; private set; }
		public void SetCapacity(long capacity) {
			Capacity = capacity;
		}
		public long Size => 0;
		public long Count => 0;
	}
}
