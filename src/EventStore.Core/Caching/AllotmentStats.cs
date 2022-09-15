namespace EventStore.Core.Caching {
	public struct AllotmentStats {
		public string Key { get; }
		public string Name { get; }
		public long Capacity { get; }
		public long Size { get; }
		public double UtilizationPercent => 100.0 * Size / Capacity;

		public AllotmentStats(string key, string name, long capacity, long size) {
			Key = key;
			Name = name;
			Capacity = capacity;
			Size = size;
		}
	}
}
