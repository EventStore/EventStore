namespace EventStore.Core.Data {
	public enum LruType {
		Normal,
		ConcurrentDictionary,
	}

	public static class PerformanceSettings {
		public static LruType ConcurrentLru { get; } = LruType.ConcurrentDictionary;
	}
}
