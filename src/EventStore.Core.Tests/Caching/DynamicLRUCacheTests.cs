using System;
using EventStore.Core.Caching;
using NUnit.Framework;

namespace EventStore.Core.Tests.Caching {
	[TestFixture]
	public class DynamicLRUCacheTests {

		private static DynamicLRUCache<TKey, TValue> GenSut<TKey, TValue> (int capacity,
			Func<TKey, TValue, int> calculateSize = null) => new(capacity, calculateSize);

		[Test]
		public void can_increase_capacity() {
			var sut = GenSut<int, int>(6, (k, v) => k + v);

			sut.Put(1, 0);
			sut.Put(2, 0);
			sut.Put(3, 0);

			Assert.AreEqual(6, sut.Size);

			sut.Resize(10, out var removedCount, out var removedSize);

			// no items removed
			Assert.AreEqual(0, removedCount);
			Assert.AreEqual(0, removedSize);

			Assert.AreEqual(6, sut.Size);
			Assert.AreEqual(10, sut.Capacity);
		}

		[Test]
		public void can_decrease_capacity() {
			var sut = GenSut<int, int>(6, (k, v) => k + v);

			sut.Put(1, 0);
			sut.Put(2, 0);
			sut.Put(3, 0);

			Assert.AreEqual(6, sut.Size);

			sut.Resize(3, out var removedCount, out var removedSize);

			// 1 & 2 should be removed
			Assert.AreEqual(2, removedCount);
			Assert.AreEqual(3, removedSize);

			Assert.AreEqual(3, sut.Size);
			Assert.AreEqual(3, sut.Capacity);
		}

		[Test]
		public void can_resize_to_zero() {
			var sut = GenSut<int, int>(6, (k, v) => k + v);

			sut.Put(1, 0);
			sut.Put(2, 0);
			sut.Put(3, 0);

			Assert.AreEqual(6, sut.Size);

			sut.Resize(0, out var removedCount, out var removedSize);

			// all items should be removed
			Assert.AreEqual(3, removedCount);
			Assert.AreEqual(6, removedSize);

			Assert.AreEqual(0, sut.Size);
			Assert.AreEqual(0, sut.Capacity);
		}

		[Test]
		public void throws_when_resizing_to_negative_capacity() {
			var sut = GenSut<int, int>(6, (k, v) => k + v);

			Assert.Throws<ArgumentOutOfRangeException>(() =>
				sut.Resize(-1, out _, out _));
		}
	}
}
