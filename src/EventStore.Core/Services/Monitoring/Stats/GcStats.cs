namespace EventStore.Core.Services.Monitoring.Stats {
	public class GcStats {
		public readonly long Gen0ItemsCount;
		public readonly long Gen1ItemsCount;
		public readonly long Gen2ItemsCount;
		public readonly long Gen0Size;
		public readonly long Gen1Size;
		public readonly long Gen2Size;
		public readonly long LargeHeapSize;
		public readonly float AllocationSpeed;
		public readonly float TimeInGc;
		public readonly long TotalBytesInHeaps;

		public GcStats(long gcGen0Items,
			long gcGen1Items,
			long gcGen2Items,
			long gcGen0Size,
			long gcGen1Size,
			long gcGen2Size,
			long gcLargeHeapSize,
			float gcAllocationSpeed,
			float gcTimeInGc,
			long gcTotalBytesInHeaps) {
			Gen0ItemsCount = gcGen0Items;
			Gen1ItemsCount = gcGen1Items;
			Gen2ItemsCount = gcGen2Items;
			Gen0Size = gcGen0Size;
			Gen1Size = gcGen1Size;
			Gen2Size = gcGen2Size;
			LargeHeapSize = gcLargeHeapSize;
			AllocationSpeed = gcAllocationSpeed;
			TimeInGc = gcTimeInGc;
			TotalBytesInHeaps = gcTotalBytesInHeaps;
		}
	}
}
