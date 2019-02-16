using System;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage {
	[TestFixture]
	public class when_caching_read_items {
		private readonly Guid _id = Guid.NewGuid();

		[Test]
		public void the_item_can_be_read() {
			var cache = new DictionaryBasedCache();
			cache.PutRecord(12000, new PrepareLogRecord(12000, _id, _id, 12000, 0, "test", 1, DateTime.UtcNow,
				PrepareFlags.None, "type", new byte[0], new byte[0]));
			PrepareLogRecord read;
			Assert.IsTrue(cache.TryGetRecord(12000, out read));
			Assert.AreEqual(_id, read.EventId);
		}

		[Test]
		public void cache_removes_oldest_item_when_max_count_reached() {
			var cache = new DictionaryBasedCache(9, 1024 * 1024 * 16);
			for (int i = 0; i < 10; i++)
				cache.PutRecord(i, new PrepareLogRecord(0, Guid.NewGuid(), _id, 0, 0, "test", 1, DateTime.UtcNow,
					PrepareFlags.None, "type", new byte[0], new byte[0]));
			PrepareLogRecord read;
			Assert.IsFalse(cache.TryGetRecord(0, out read));
		}

		[Test]
		public void cache_removes_oldest_item_when_max_size_reached_by_data() {
			var cache = new DictionaryBasedCache(100, 1024 * 9);
			for (int i = 0; i < 10; i++)
				cache.PutRecord(i, new PrepareLogRecord(0, Guid.NewGuid(), _id, 0, 0, "test", 1, DateTime.UtcNow,
					PrepareFlags.None, "type", new byte[1024], new byte[0]));
			PrepareLogRecord read;
			Assert.IsFalse(cache.TryGetRecord(0, out read));
		}

		[Test]
		public void cache_removes_oldest_item_when_max_size_reached_metadata() {
			var cache = new DictionaryBasedCache(100, 1024 * 9);
			for (int i = 0; i < 10; i++)
				cache.PutRecord(i, new PrepareLogRecord(0, Guid.NewGuid(), _id, 0, 0, "test", 1, DateTime.UtcNow,
					PrepareFlags.None, "type", new byte[0], new byte[1024]));
			PrepareLogRecord read;
			Assert.IsFalse(cache.TryGetRecord(0, out read));
		}

		[Test]
		public void empty_cache_has_zeroed_statistics() {
			var cache = new DictionaryBasedCache(100, 1024 * 9);
			var stats = cache.GetStatistics();
			Assert.AreEqual(0, stats.MissCount);
			Assert.AreEqual(0, stats.HitCount);
			Assert.AreEqual(0, stats.Size);
			Assert.AreEqual(0, stats.Count);
		}

		[Test]
		public void statistics_are_updated_with_hits() {
			var cache = new DictionaryBasedCache(100, 1024 * 9);
			cache.PutRecord(1, new PrepareLogRecord(1, Guid.NewGuid(), _id, 1, 0, "test", 1, DateTime.UtcNow,
				PrepareFlags.None, "type", new byte[0], new byte[1024]));
			PrepareLogRecord read;
			cache.TryGetRecord(1, out read);
			Assert.AreEqual(1, cache.GetStatistics().HitCount);
		}

		[Test]
		public void statistics_are_updated_with_misses() {
			var cache = new DictionaryBasedCache(100, 1024 * 9);
			cache.PutRecord(1, new PrepareLogRecord(1, Guid.NewGuid(), _id, 1, 0, "test", 1, DateTime.UtcNow,
				PrepareFlags.None, "type", new byte[0], new byte[1024]));
			PrepareLogRecord read;
			cache.TryGetRecord(0, out read);
			Assert.AreEqual(1, cache.GetStatistics().MissCount);
		}

		[Test]
		public void statistics_are_updated_with_total_count() {
			var cache = new DictionaryBasedCache(100, 1024 * 9);
			cache.PutRecord(1, new PrepareLogRecord(1, Guid.NewGuid(), _id, 1, 0, "test", 1, DateTime.UtcNow,
				PrepareFlags.None, "type", new byte[0], new byte[1024]));
			PrepareLogRecord read;
			cache.TryGetRecord(0, out read);
			Assert.AreEqual(1, cache.GetStatistics().Count);
		}

		[Test]
		public void statistics_are_updated_with_total_size() {
			var cache = new DictionaryBasedCache(100, 1024 * 9);
			var record = new PrepareLogRecord(1, Guid.NewGuid(), _id, 1, 0, "test", 1, DateTime.UtcNow,
				PrepareFlags.None, "type", new byte[0], new byte[1024]);
			cache.PutRecord(1, record);
			PrepareLogRecord read;
			cache.TryGetRecord(0, out read);
			Assert.AreEqual(record.InMemorySize, cache.GetStatistics().Size);
		}
	}
}
