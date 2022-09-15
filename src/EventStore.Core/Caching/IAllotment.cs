namespace EventStore.Core.Caching {
	// This has its capacity adjusted by the IAllotmentResizer
	public interface IAllotment {
		string Name { get; }
		long Capacity { get; set; }
		long Size { get; }
	}
}
