using System;
using EventStore.Core.Caching;
using NUnit.Framework;

namespace EventStore.Core.Tests.Caching {
	[TestFixture]
	public class CacheResizerTests {
		[Test]
		public void dynamic_cache_resizer_loopback() {
			var cacheResizer = new DynamicCacheResizer("", 10, 12, EmptyAllotment.Instance);
			Assert.AreEqual(12, cacheResizer.Weight);
		}

		[Test]
		public void static_cache_resizer_loopback() {
			var cacheResizer = new StaticCacheResizer("", 10, EmptyAllotment.Instance);
			Assert.AreEqual(0, cacheResizer.Weight);
		}

		[Test]
		public void dynamic_cache_resizer_with_zero_weight_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
					new DynamicCacheResizer("", 0, 0, EmptyAllotment.Instance));

		[Test]
		public void dynamic_cache_resizer_with_negative_weight_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new DynamicCacheResizer("", 0, -1, EmptyAllotment.Instance));

		[Test]
		public void dynamic_cache_resizer_with_negative_mem_allotment_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new DynamicCacheResizer("", -1, 10, EmptyAllotment.Instance));

		[Test]
		public void static_cache_resizer_with_negative_mem_allotment_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new StaticCacheResizer("", -1, EmptyAllotment.Instance));

		[Test]
		public void static_calculates_capacity_correctly() {
			var allotment = new EmptyAllotment();

			var sut = new StaticCacheResizer("bytes", 1000, allotment);

			sut.CalcCapacity(2000, 100);
			Assert.AreEqual(1000, allotment.Capacity);

			sut.CalcCapacity(200, 100);
			Assert.AreEqual(1000, allotment.Capacity);
		}

		[Test]
		public void dynamic_calculates_capacity_correctly() {
			var allotment = new EmptyAllotment();

			var sut = new DynamicCacheResizer("bytes", 1000, 50, allotment);

			sut.CalcCapacity(4000, 100);
			Assert.AreEqual(2000, allotment.Capacity);

			sut.CalcCapacity(200, 100);
			Assert.AreEqual(1000, allotment.Capacity);
		}

		[Test]
		public void composite_calculates_capacity_correctly_static() {
			var allotmentA = new EmptyAllotment();
			var allotmentB = new EmptyAllotment();

			var sut = new CompositeCacheResizer("root", "bytes", 100,
				new StaticCacheResizer("bytes", 1000, allotmentA),
				new StaticCacheResizer("bytes", 2000, allotmentB));

			sut.CalcCapacity(4000, 100);
			Assert.AreEqual(1000, allotmentA.Capacity);
			Assert.AreEqual(2000, allotmentB.Capacity);

			sut.CalcCapacity(200, 100);
			Assert.AreEqual(1000, allotmentA.Capacity);
			Assert.AreEqual(2000, allotmentB.Capacity);
		}

		[Test]
		public void composite_calculates_capacity_correctly_dynamic() {
			var allotmentA = new EmptyAllotment();
			var allotmentB = new EmptyAllotment();

			var sut = new CompositeCacheResizer("root", "bytes", 100,
				new DynamicCacheResizer("bytes", 3000, 40, allotmentA),
				new DynamicCacheResizer("bytes", 1000, 60, allotmentB));

			sut.CalcCapacity(10_000, 100);
			Assert.AreEqual(4000, allotmentA.Capacity);
			Assert.AreEqual(6000, allotmentB.Capacity);

			sut.CalcCapacity(200, 100);
			Assert.AreEqual(3000, allotmentA.Capacity);
			Assert.AreEqual(1000, allotmentB.Capacity);

		}
	}
}
