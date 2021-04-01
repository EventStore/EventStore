namespace EventStore.Core.Data {
	public enum LruType {
		Normal,
		ConcurrentDictionary,
	}

	public static class PerformanceSettings {
		public static LruType ConcurrentLru { get; } = LruType.ConcurrentDictionary;
		public static int PrepopulateCaches { get; } = 0; // 2_000_000;
	}
}
