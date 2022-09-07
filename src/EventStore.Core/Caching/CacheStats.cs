namespace EventStore.Core.Caching {
	public struct CacheStats: ICacheStats {
		public string Key { get; }
		public string Name { get; }
		public int Weight { get; }
		public long MemAllotted { get; }
		public long MemUsed { get; }

		public CacheStats(string key, string name, int weight, long memAllotted, long memUsed) {
			Key = key;
			Name = name;
			Weight = weight;
			MemAllotted = memAllotted;
			MemUsed = memUsed;
		}
	}
}
