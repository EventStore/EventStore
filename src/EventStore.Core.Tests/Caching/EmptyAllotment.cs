using EventStore.Core.Caching;

namespace EventStore.Core.Tests.Caching {
	public class EmptyAllotment : IAllotment {
		public static EmptyAllotment Instance { get; } = new();
		public long Capacity { get; set; }
		public long Size => 0;
	}
}
