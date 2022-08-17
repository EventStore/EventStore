using System;
using EventStore.Core.Caching;
using NUnit.Framework;

namespace EventStore.Core.Tests.Caching {
	[TestFixture]
	public class CacheSettingsTest {
		[Test]
		public void dynamic_cache_settings_loopback() {
			var cacheSettings = CacheSettings.Dynamic("test", 10, 12);

			Assert.AreEqual("test", cacheSettings.Name);
			Assert.True(cacheSettings.IsDynamic);
			Assert.AreEqual(12, cacheSettings.Weight);
			Assert.AreEqual(10, cacheSettings.MinMemAllocation);
			Assert.Throws<InvalidOperationException>(() => {
				var x = cacheSettings.InitialMaxMemAllocation;
			});
			Assert.Throws<InvalidOperationException>(() => cacheSettings.GetMemoryUsage());
			Assert.Throws<InvalidOperationException>(() => cacheSettings.UpdateMaxMemoryAllocation(10));
		}

		[Test]
		public void static_cache_settings_loopback() {
			var cacheSettings = CacheSettings.Static("test", 10);

			Assert.AreEqual("test", cacheSettings.Name);
			Assert.False(cacheSettings.IsDynamic);
			Assert.AreEqual(0, cacheSettings.Weight);
			Assert.AreEqual(10, cacheSettings.MinMemAllocation);
			Assert.AreEqual(10, cacheSettings.InitialMaxMemAllocation);
			Assert.Throws<InvalidOperationException>(() => cacheSettings.GetMemoryUsage());
			Assert.Throws<InvalidOperationException>(() => cacheSettings.UpdateMaxMemoryAllocation(10));
		}

		[Test]
		public void dynamic_cache_settings_with_zero_weight_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
					CacheSettings.Dynamic("test", 0, 0));

		[Test]
		public void dynamic_cache_settings_with_negative_weight_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				CacheSettings.Dynamic("test", 0, -1));

		[Test]
		public void dynamic_cache_settings_with_negative_mem_allocation_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				CacheSettings.Dynamic("test", -1, 10));

		[Test]
		public void static_cache_settings_with_negative_mem_allocation_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				CacheSettings.Static("test", -1));
	}
}
