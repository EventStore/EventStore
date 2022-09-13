namespace EventStore.Core.Caching {
	public interface IAllotment {
		string Name { get; }
		long Capacity { get; set; }
		long Size { get; }
	}
}
