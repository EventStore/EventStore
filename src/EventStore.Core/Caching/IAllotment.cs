namespace EventStore.Core.Caching {
	public interface IAllotment {
		long Capacity { get; set; }
		long Size { get; }
	}
}
