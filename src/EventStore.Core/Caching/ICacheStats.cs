namespace EventStore.Core.Caching {
	public interface ICacheStats {
		string Key { get; }
		string Name { get; }
		int Weight { get; }
		long MemAllotted { get; }
		long MemUsed { get; }
	}
}
