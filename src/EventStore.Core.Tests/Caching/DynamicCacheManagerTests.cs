using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Caching;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Replication;
using NUnit.Framework;

namespace EventStore.Core.Tests.Caching {
	[TestFixture]
	public class DynamicCacheManagerTests {
		private readonly FakePublisher _fakePublisher = new();

		private DynamicCacheManager GenSut(
			Func<long> getFreeMem,
			long totalMem,
			int keepFreeMemPercent,
			long keepFreeMemBytes,
			TimeSpan monitoringInterval,
			TimeSpan minResizeInterval,
			params ICacheSettings[] cacheSettings) {
			var sut = new DynamicCacheManager(
				_fakePublisher,
				getFreeMem,
				totalMem,
				keepFreeMemPercent,
				keepFreeMemBytes,
				monitoringInterval,
				minResizeInterval,
				cacheSettings);

			_fakePublisher.Messages.Clear();

			return sut;
		}

		private async Task TickPublished() {
			while (!_fakePublisher.Messages.ToArray().Any(x => x is TimerMessage.Schedule))
				await Task.Delay(10);

			_fakePublisher.Messages.Clear();
		}

		[Test]
		public async Task ticks() {
			var sut = GenSut(
				() => 100,
				100,
				0,
				0,
				TimeSpan.MaxValue,
				TimeSpan.MaxValue);

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());
			await TickPublished().WithTimeout(TimeSpan.FromSeconds(10));
		}

		[Test]
		public void initial_allocation_set_according_to_weight() {
			var cache1 = CacheSettings.Dynamic("cache1", 0, 30);
			var cache2 = CacheSettings.Dynamic("cache2", 0, 70);
			var cache3 = CacheSettings.Static("cache3", 5);

			GenSut(
				() => 10,
				12,
				0,
				0,
				TimeSpan.MaxValue,
				TimeSpan.MaxValue,
				cache1,
				cache2,
				cache3);

			Assert.AreEqual(3, cache1.InitialMaxMemAllocation);
			Assert.AreEqual(7, cache2.InitialMaxMemAllocation);
			Assert.AreEqual(5, cache3.InitialMaxMemAllocation);
		}

		[TestCase(50, 0, 4)]
		[TestCase(0, 4, 6)]
		public void initial_allocation_set_according_to_keep_free_mem(int percent, long bytes, long expected) {
			var cache = CacheSettings.Dynamic("cache", 0, 100);

			GenSut(
				() => 10,
				12,
				percent,
				bytes,
				TimeSpan.MaxValue,
				TimeSpan.MaxValue,
				cache);

			Assert.AreEqual(expected, cache.InitialMaxMemAllocation);
		}


		[TestCase(20, 0)]
		[TestCase(0, 20)]
		public async Task caches_resized_when_memory_below_keep_free_mem(int percent, long bytes) {
			var cache1 = CacheSettings.Dynamic("cache1", 1, 60);
			var cache2 = CacheSettings.Dynamic("cache2", 2, 40);
			long cache1Mem = -1, cache2Mem = -1;

			cache1.GetMemoryUsage = () => 0;
			cache1.UpdateMaxMemoryAllocation = mem => Interlocked.Exchange(ref cache1Mem, mem);

			cache2.GetMemoryUsage = () => 0;
			cache2.UpdateMaxMemoryAllocation = mem => Interlocked.Exchange(ref cache2Mem, mem);

			var request = 0;
			var freeMem = new[] { 100, 19 };

			var sut = GenSut(
				() => freeMem[request++],
				100,
				percent,
				bytes,
				TimeSpan.MaxValue,
				TimeSpan.MaxValue,
				cache1,
				cache2);

			Assert.AreEqual(48, cache1.InitialMaxMemAllocation);
			Assert.AreEqual(32, cache2.InitialMaxMemAllocation);

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());

			await TickPublished();

			// caches resized to minimum amount
			Assert.AreEqual(1, Interlocked.Read(ref cache1Mem));
			Assert.AreEqual(2, Interlocked.Read(ref cache2Mem));
		}

		[Test]
		public async Task caches_resized_after_min_resize_interval() {
			var cache1 = CacheSettings.Dynamic("cache1", 1, 60);
			var cache2 = CacheSettings.Dynamic("cache2", 2, 40);
			long cache1Mem = -1, cache2Mem = -1;

			cache1.GetMemoryUsage = () => 0;
			cache1.UpdateMaxMemoryAllocation = mem => Interlocked.Exchange(ref cache1Mem, mem);

			cache2.GetMemoryUsage = () => 0;
			cache2.UpdateMaxMemoryAllocation = mem => Interlocked.Exchange(ref cache2Mem, mem);

			var request = 0;
			var freeMem = new[] { 100, 90 };

			var sut = GenSut(
				() => freeMem[request++],
				100,
				0,
				0,
				TimeSpan.MaxValue,
				TimeSpan.Zero,
				cache1,
				cache2);

			Assert.AreEqual(60, cache1.InitialMaxMemAllocation);
			Assert.AreEqual(40, cache2.InitialMaxMemAllocation);

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());

			await TickPublished();

			// caches resized according to 90% free memory
			Assert.AreEqual(54, Interlocked.Read(ref cache1Mem));
			Assert.AreEqual(36, Interlocked.Read(ref cache2Mem));
		}

		[Test]
		public async Task caches_not_resized_when_conditions_not_met() {
			var cache1 = CacheSettings.Dynamic("cache1", 1, 60);
			var cache2 = CacheSettings.Dynamic("cache2", 2, 40);

			int numResize = 0;

			cache1.GetMemoryUsage = () => 0;
			cache1.UpdateMaxMemoryAllocation = _ => Interlocked.Increment(ref numResize);

			cache2.GetMemoryUsage = () => 0;
			cache2.UpdateMaxMemoryAllocation = _ => Interlocked.Increment(ref numResize);

			var request = 0;
			var freeMem = new[] { 100, 90 };

			var sut = GenSut(
				() => freeMem[request++],
				100,
				89,
				89,
				TimeSpan.FromSeconds(1),
				TimeSpan.FromMinutes(1),
				cache1,
				cache2);

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());
			await TickPublished();

			Assert.AreEqual(0, Volatile.Read(ref numResize));
		}

		[Test]
		public async Task caches_resized_only_when_calculated_allocation_changes() {
			var cache = CacheSettings.Dynamic("cache", 0, 100);

			var numResize = 0;
			var cachedMem = 0L;

			cache.GetMemoryUsage = () => 0;
			cache.UpdateMaxMemoryAllocation = mem => {
				Interlocked.Increment(ref numResize);
				Interlocked.Exchange(ref cachedMem, mem);
			};

			var request = 0;
			var freeMem = new[] { 100, 90, 90, 80 };

			var sut = GenSut(
				() => freeMem[request++],
				100,
				0,
				0,
				TimeSpan.MaxValue,
				TimeSpan.Zero,
				cache);

			Assert.AreEqual(100, cache.InitialMaxMemAllocation);

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());
			await TickPublished();
			Assert.AreEqual(90, Volatile.Read(ref cachedMem));
			Assert.AreEqual(1, Volatile.Read(ref numResize));

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());
			await TickPublished();
			Assert.AreEqual(90, Volatile.Read(ref cachedMem));
			Assert.AreEqual(1, Volatile.Read(ref numResize));

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());
			await TickPublished();
			Assert.AreEqual(80, Volatile.Read(ref cachedMem));
			Assert.AreEqual(2, Volatile.Read(ref numResize));
		}

		[Test]
		public void correct_stats_are_produced() {
			var cache1 = CacheSettings.Dynamic("test1", 10, 100);
			var cache2 = CacheSettings.Static("test2", 15);

			cache1.GetMemoryUsage = () => 12;
			cache2.GetMemoryUsage = () => 10;

			var sut = GenSut(
				() => 80,
				100,
				0,
				0,
				TimeSpan.MaxValue,
				TimeSpan.MaxValue,
				cache1,
				cache2);

			var envelope = new FakeEnvelope();
			sut.Handle(new MonitoringMessage.InternalStatsRequest(envelope));

			Assert.AreEqual(1, envelope.Replies.Count);

			var msg = (MonitoringMessage.InternalStatsRequestResponse) envelope.Replies.First();
			var expectedStats = new Dictionary<string, object> {
				{"es-cache-test1-name", "test1"},
				{"es-cache-test1-weight", 100},
				{"es-cache-test1-mem-used", 12L},
				{"es-cache-test1-mem-minAlloc", 10L},
				{"es-cache-test1-mem-maxAlloc", 80L},

				{"es-cache-test2-name", "test2"},
				{"es-cache-test2-weight", 0},
				{"es-cache-test2-mem-used", 10L},
				{"es-cache-test2-mem-minAlloc", 15L},
				{"es-cache-test2-mem-maxAlloc", 15L}
			};

			AssertEx.AssertUsingDeepCompare(msg.Stats, expectedStats);
		}
	}
}
