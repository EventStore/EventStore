namespace EventStore.Core.Services.Storage.ReaderIndex {
	public class ReadCacheStats {
		public readonly long Size;
		public readonly int Count;
		public readonly long HitCount;
		public readonly long MissCount;

		public ReadCacheStats(long size, int count, long hitCount, long missCount) {
			Size = size;
			Count = count;
			HitCount = hitCount;
			MissCount = missCount;
		}
	}
}
