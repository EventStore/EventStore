using System;
using EventStore.Core.Caching;
using NUnit.Framework;

namespace EventStore.Core.Tests.Caching {
	[TestFixture]
	public class AllotmentResizerTests {
		[Test]
		public void dynamic_cache_resizer_loopback() {
			var cacheResizer = new DynamicAllotmentResizer(ResizerUnit.Bytes, 10, 12, EmptyAllotment.Instance);
			Assert.AreEqual(12, cacheResizer.Weight);
		}

		[Test]
		public void static_cache_resizer_loopback() {
			var cacheResizer = new StaticAllotmentResizer(ResizerUnit.Bytes, 10, EmptyAllotment.Instance);
			Assert.AreEqual(0, cacheResizer.Weight);
		}

		[Test]
		public void dynamic_cache_resizer_with_zero_weight_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
					new DynamicAllotmentResizer(ResizerUnit.Bytes, 0, 0, EmptyAllotment.Instance));

		[Test]
		public void dynamic_cache_resizer_with_negative_weight_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new DynamicAllotmentResizer(ResizerUnit.Bytes, 0, -1, EmptyAllotment.Instance));

		[Test]
		public void dynamic_cache_resizer_with_negative_mem_allotment_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new DynamicAllotmentResizer(ResizerUnit.Bytes, -1, 10, EmptyAllotment.Instance));

		[Test]
		public void static_cache_resizer_with_negative_mem_allotment_throws() =>
			Assert.Throws<ArgumentOutOfRangeException>(() =>
				new StaticAllotmentResizer(ResizerUnit.Bytes, -1, EmptyAllotment.Instance));

		[Test]
		public void composite_cache_resizer_with_mixed_units_throws() =>
			Assert.Throws<ArgumentException>(() =>
				new CompositeAllotmentResizer("root", 100,
					new StaticAllotmentResizer(ResizerUnit.Bytes, 10, EmptyAllotment.Instance),
					new StaticAllotmentResizer(ResizerUnit.Entries, 10, EmptyAllotment.Instance)));

		[Test]
		public void static_calculates_capacity_correctly() {
			var allotment = new EmptyAllotment();

			var sut = new StaticAllotmentResizer(ResizerUnit.Bytes, 1000, allotment);

			sut.CalcCapacityTopLevel(2000);
			Assert.AreEqual(1000, allotment.Capacity);

			sut.CalcCapacityTopLevel(200);
			Assert.AreEqual(1000, allotment.Capacity);
		}

		[Test]
		public void dynamic_calculates_capacity_correctly() {
			var allotment = new EmptyAllotment();

			var sut = new DynamicAllotmentResizer(ResizerUnit.Bytes, 1000, 50, allotment);

			sut.CalcCapacityTopLevel(4000);
			Assert.AreEqual(4000, allotment.Capacity);

			sut.CalcCapacityTopLevel(200);
			Assert.AreEqual(1000, allotment.Capacity);
		}

		[Test]
		public void composite_calculates_capacity_correctly_static() {
			var allotmentA = new EmptyAllotment();
			var allotmentB = new EmptyAllotment();

			var sut = new CompositeAllotmentResizer("root", 100,
				new StaticAllotmentResizer(ResizerUnit.Bytes, 1000, allotmentA),
				new StaticAllotmentResizer(ResizerUnit.Bytes, 2000, allotmentB));

			sut.CalcCapacityTopLevel(4000);
			Assert.AreEqual(1000, allotmentA.Capacity);
			Assert.AreEqual(2000, allotmentB.Capacity);

			sut.CalcCapacityTopLevel(200);
			Assert.AreEqual(1000, allotmentA.Capacity);
			Assert.AreEqual(2000, allotmentB.Capacity);
		}

		[Test]
		public void composite_calculates_capacity_correctly_dynamic() {
			var allotmentA = new EmptyAllotment();
			var allotmentB = new EmptyAllotment();

			var sut = new CompositeAllotmentResizer("root", 100,
				new DynamicAllotmentResizer(ResizerUnit.Bytes, 3000, 40, allotmentA),
				new DynamicAllotmentResizer(ResizerUnit.Bytes, 1000, 60, allotmentB));

			sut.CalcCapacityTopLevel(10_000);
			Assert.AreEqual(4000, allotmentA.Capacity);
			Assert.AreEqual(6000, allotmentB.Capacity);

			// nb: we overflow the capacity available in order to meet the minimums
			sut.CalcCapacityTopLevel(5000);
			Assert.AreEqual(3000, allotmentA.Capacity); // <-- minimum
			Assert.AreEqual(3000, allotmentB.Capacity); // <-- 60% of 5000

			sut.CalcCapacityTopLevel(200);
			Assert.AreEqual(3000, allotmentA.Capacity);
			Assert.AreEqual(1000, allotmentB.Capacity);

		}

		[Test]
		public void composite_calculates_capacity_correctly_mixed() {
			var allotmentA = new EmptyAllotment();
			var allotmentB = new EmptyAllotment();

			var sut = new CompositeAllotmentResizer("root", 100,
				new StaticAllotmentResizer(ResizerUnit.Bytes, 1000, allotmentA),
				new DynamicAllotmentResizer(ResizerUnit.Bytes, 1000, 60, allotmentB));

			sut.CalcCapacityTopLevel(10_000);
			Assert.AreEqual(1000, allotmentA.Capacity);
			Assert.AreEqual(9000, allotmentB.Capacity);
		}

		[Test]
		public void composite_calculates_capacity_correctly_complex() {
			var allotmentA = new EmptyAllotment();
			var allotmentB = new EmptyAllotment();
			var allotmentC = new EmptyAllotment();
			var allotmentD = new EmptyAllotment();

			// root
			//  -> static 1000                 A
			//  -> composite
			//      -> static 1000             B
			//      -> dynamic 60% min 1000    C
			//      -> dynamic 40% min 1000    D

			var sut = new CompositeAllotmentResizer(
				name: "root",
				weight: 100,
				new StaticAllotmentResizer(
					unit: ResizerUnit.Bytes,
					capacity: 1000,
					allotment: allotmentA),
				new CompositeAllotmentResizer(
					name: "composite",
					weight: 50,
					new StaticAllotmentResizer(
						unit: ResizerUnit.Bytes,
						capacity: 1000,
						allotment: allotmentB),
					new DynamicAllotmentResizer(
						unit: ResizerUnit.Bytes,
						minCapacity: 1000,
						weight: 60,
						allotment: allotmentC),
					new DynamicAllotmentResizer(
						unit: ResizerUnit.Bytes,
						minCapacity: 1000,
						weight: 40,
						allotment: allotmentD)));

			// lots of space
			sut.CalcCapacityTopLevel(12_000);

			Assert.AreEqual(1000, allotmentA.Capacity);
			Assert.AreEqual(1000, allotmentB.Capacity);
			Assert.AreEqual(6000, allotmentC.Capacity);
			Assert.AreEqual(4000, allotmentD.Capacity);

			// low space
			sut.CalcCapacityTopLevel(1_000);

			Assert.AreEqual(1000, allotmentA.Capacity);
			Assert.AreEqual(1000, allotmentB.Capacity);
			Assert.AreEqual(1000, allotmentC.Capacity);
			Assert.AreEqual(1000, allotmentD.Capacity);
		}
	}
}
