using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public class DictionaryBasedCache : BoundedCache<long, PrepareLogRecord> {
		public DictionaryBasedCache() : this(1000000, 128 * 1024 * 1024) {
		}

		public DictionaryBasedCache(int maxCachedEntries, long maxCacheSize)
			: base(maxCachedEntries, maxCacheSize, x => x.InMemorySize) {
		}
	}
}
