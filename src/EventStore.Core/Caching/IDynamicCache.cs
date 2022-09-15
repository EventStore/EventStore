namespace EventStore.Core.Caching {
	// This has its capacity adjusted by the ICacheResizer
	public interface IDynamicCache {
		string Name { get; }
		long Capacity { get; set; }
		long Size { get; }
		// todo: hits and misses
	}
}
