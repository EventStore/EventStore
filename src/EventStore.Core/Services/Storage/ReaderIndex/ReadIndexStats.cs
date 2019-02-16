namespace EventStore.Core.Services.Storage.ReaderIndex {
	public class ReadIndexStats {
		public readonly long CachedRecordReads;
		public readonly long NotCachedRecordReads;
		public readonly long CachedStreamInfoReads;
		public readonly long NotCachedStreamInfoReads;
		public readonly long HashCollisions;
		public readonly long CachedTransInfoReads;
		public readonly long NotCachedTransInfoReads;

		public ReadIndexStats(long cachedRecordReads, long notCachedRecordReads,
			long cachedStreamInfoReads, long notCachedStreamInfoReads,
			long hashCollisions,
			long cachedTransInfoReads, long notCachedTransInfoReads) {
			CachedRecordReads = cachedRecordReads;
			NotCachedRecordReads = notCachedRecordReads;
			CachedStreamInfoReads = cachedStreamInfoReads;
			NotCachedStreamInfoReads = notCachedStreamInfoReads;
			HashCollisions = hashCollisions;
			CachedTransInfoReads = cachedTransInfoReads;
			NotCachedTransInfoReads = notCachedTransInfoReads;
		}
	}
}
