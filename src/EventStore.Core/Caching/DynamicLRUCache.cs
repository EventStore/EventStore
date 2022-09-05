//qq seen
using System;
using System.Threading;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Caching {
	public class DynamicLRUCache<TKey, TValue> : LRUCache<TKey, TValue>, IDynamicLRUCache<TKey, TValue> {
		//qq in bytes?
		const int ResizeBatchSize = 100_000;

		public DynamicLRUCache(long capacity, Func<TKey, TValue, int> calculateItemSize)
			: base(capacity, calculateItemSize) { }

		public void Resize(long newCapacity, out int removedCount, out long removedSize) {
			if (newCapacity < 0)
				throw new ArgumentOutOfRangeException(nameof(newCapacity));

			removedCount = 0;
			removedSize = 0L;

			// when decreasing the capacity, remove items batch by batch to prevent
			// other threads from starving when trying to access the cache.
			// when increasing, jump straight up.
			var curCapacity = Interlocked.Read(ref _capacity);

			while (curCapacity != newCapacity) {
				curCapacity = Math.Max(curCapacity - ResizeBatchSize, newCapacity);
				Interlocked.Exchange(ref _capacity, curCapacity);
				EnsureCapacity(0, out var curRemovedCount, out var curRemovedSize);
				removedCount += curRemovedCount;
				removedSize += curRemovedSize;
			}
		}
	}
}
