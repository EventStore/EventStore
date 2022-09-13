using System;
using EventStore.Core.Caching;
using NUnit.Framework;

namespace EventStore.Core.Tests.Caching {
	[TestFixture]
	public class CacheResizerTests {
		[Test]
		public void dynamic_cache_resizer_loopback() {
			var cacheResizer = new DynamicCacheResizer("test", "", 10, 12, EmptyAllotment.Instance);

			Assert.AreEqual("test", cacheResizer.Name);
			Assert.AreEqual(12, cacheResizer.Weight);
		}

		[Test]
		public void static_cache_resizer_loopback() {
			var cacheResizer = new StaticCacheResizer("test", "", 10, EmptyAllotment.Instance);

			Assert.AreEqual("test", cacheResizer.Name);
			Assert.AreEqual(0, cacheResizer.Weight);
		}

		[Test]
		public void dynamic_cache_resizer_with_zero_weight_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
					new DynamicCacheResizer("test", "", 0, 0, EmptyAllotment.Instance));

		[Test]
		public void dynamic_cache_resizer_with_negative_weight_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new DynamicCacheResizer("test", "", 0, -1, EmptyAllotment.Instance));

		[Test]
		public void dynamic_cache_resizer_with_negative_mem_allotment_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new DynamicCacheResizer("test", "", -1, 10, EmptyAllotment.Instance));

		[Test]
		public void static_cache_resizer_with_negative_mem_allotment_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new StaticCacheResizer("test", "", -1, EmptyAllotment.Instance));
	}
}
