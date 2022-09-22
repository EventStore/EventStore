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
			Func<long> getFreeSystemMem,
			Func<long> getFreeHeapMem,
			long totalMem,
			int keepFreeMemPercent,
			long keepFreeMemBytes,
			TimeSpan monitoringInterval,
			TimeSpan minResizeInterval,
			long minResizeThreshold,
			ICacheResizer rootCacheResizer) {
			var sut = new DynamicCacheManager(
				_fakePublisher,
				getFreeSystemMem,
				getFreeHeapMem,
				totalMem,
				keepFreeMemPercent,
				keepFreeMemBytes,
				monitoringInterval,
				minResizeInterval,
				minResizeThreshold,
				rootCacheResizer);
			sut.Start();

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
				() => 0,
				100,
				0,
				0,
				TimeSpan.MaxValue,
				TimeSpan.MaxValue,
				0,
				new StaticCacheResizer(ResizerUnit.Bytes, 0, EmptyDynamicCache.Instance));

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());
			await TickPublished().WithTimeout(TimeSpan.FromSeconds(10));
		}

		[TestCase(20, 0)]
		[TestCase(0, 20)]
		public async Task caches_resized_when_memory_below_keep_free_mem(int percent, long bytes) {
			long cache1Mem = -1, cache2Mem = -1;
			var cache1 = new DynamicCacheResizer(ResizerUnit.Bytes, 1, 60, new AdHocDynamicCache(
				() => 0,
				mem => Interlocked.Exchange(ref cache1Mem, mem)));
			var cache2 = new DynamicCacheResizer(ResizerUnit.Bytes, 2, 40, new AdHocDynamicCache(
				() => 0,
				mem => Interlocked.Exchange(ref cache2Mem, mem)));

			var request = 0;
			var freeMem = new[] { 100, 19 };

			var sut = GenSut(
				() => freeMem[request++],
				() => 0,
				100,
				percent,
				bytes,
				TimeSpan.MaxValue,
				TimeSpan.MaxValue,
				0,
				new CompositeCacheResizer("root", 100, cache1, cache2));

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());

			await TickPublished();

			// caches resized to minimum amount
			Assert.AreEqual(1, Interlocked.Read(ref cache1Mem));
			Assert.AreEqual(2, Interlocked.Read(ref cache2Mem));
		}

		[Test]
		public async Task caches_resized_after_min_resize_interval() {
			long cache1Mem = -1, cache2Mem = -1;
			var cache1 = new DynamicCacheResizer(ResizerUnit.Bytes, 1, 60, new AdHocDynamicCache(
				() => 0,
				mem => Interlocked.Exchange(ref cache1Mem, mem)));
			var cache2 = new DynamicCacheResizer(ResizerUnit.Bytes, 2, 40, new AdHocDynamicCache(
				() => 0,
				mem => Interlocked.Exchange(ref cache2Mem, mem)));

			var request = 0;
			var freeMem = new[] { 100, 90 };

			var sut = GenSut(
				() => freeMem[request++],
				() => 0,
				100,
				0,
				0,
				TimeSpan.MaxValue,
				TimeSpan.Zero,
				0,
				new CompositeCacheResizer("root", 100, cache1, cache2));

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());

			await TickPublished();

			// caches resized according to 90% free memory
			Assert.AreEqual(54, Interlocked.Read(ref cache1Mem));
			Assert.AreEqual(36, Interlocked.Read(ref cache2Mem));
		}

		[Test]
		public async Task caches_not_resized_when_conditions_not_met() {
			int numResize = 0;
			var cache = new AdHocDynamicCache(
				() => 0,
				_ => Interlocked.Increment(ref numResize));
			var cache1 = new DynamicCacheResizer(ResizerUnit.Bytes, 1, 60, cache);
			var cache2 = new DynamicCacheResizer(ResizerUnit.Bytes, 2, 40, cache);

			var request = 0;
			var freeMem = new[] { 100, 90 };

			var sut = GenSut(
				() => freeMem[request++],
				() => 0,
				100,
				89,
				89,
				TimeSpan.FromSeconds(1),
				TimeSpan.FromMinutes(1),
				0,
				new CompositeCacheResizer("root", 100, cache1, cache2));

			sut.Handle(new MonitoringMessage.DynamicCacheManagerTick());
			await TickPublished();

			// sized once each to start with
			Assert.AreEqual(2, Volatile.Read(ref numResize));
		}

		[Test]
		public void correct_stats_are_produced() {
			var cache1 = new DynamicCacheResizer(ResizerUnit.Bytes, 10, 100, new AdHocDynamicCache(
				() => 12,
				mem => { },
				"test1"));
			var cache2 = new StaticCacheResizer(ResizerUnit.Bytes, 15, new AdHocDynamicCache(
				() => 10,
				mem => { },
				"test2"));

			var sut = GenSut(
				() => 80,
				() => 0,
				100,
				0,
				0,
				TimeSpan.MaxValue,
				TimeSpan.MaxValue,
				0,
				new CompositeCacheResizer("root", 123, cache1, cache2));

			var envelope = new FakeEnvelope();
			sut.Handle(new MonitoringMessage.InternalStatsRequest(envelope));

			Assert.AreEqual(1, envelope.Replies.Count);

			var msg = (MonitoringMessage.InternalStatsRequestResponse) envelope.Replies.First();
			var expectedStats = new Dictionary<string, object> {
				{"es-cache-root-name", "root"},
				{"es-cache-root-sizeBytes", 22L},
				{"es-cache-root-capacityBytes", 80L},
				{"es-cache-root-utilizationPercent", 100.0 * 22 / 80},

				{"es-cache-root-test1-name", "test1"},
				{"es-cache-root-test1-sizeBytes", 12L},
				{"es-cache-root-test1-capacityBytes", 65L},
				{"es-cache-root-test1-utilizationPercent", 100.0 * 12 / 65},

				{"es-cache-root-test2-name", "test2"},
				{"es-cache-root-test2-sizeBytes", 10L},
				{"es-cache-root-test2-capacityBytes", 15L},
				{"es-cache-root-test2-utilizationPercent", 100.0 * 10 / 15},
			};

			AssertEx.AssertUsingDeepCompare(msg.Stats, expectedStats);
		}
	}
}
