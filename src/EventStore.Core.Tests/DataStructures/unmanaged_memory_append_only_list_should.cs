using System;
using EventStore.Core.DataStructures;
using NUnit.Framework;

namespace EventStore.Core.Tests.DataStructures {
	[TestFixture(1)]
	[TestFixture(2)]
	[TestFixture(10)]
	[TestFixture(13)]
	[TestFixture(100)]
	public class unmanaged_memory_append_only_list_should {
		private UnmanagedMemoryAppendOnlyList<int> _list;
		private readonly int _maxCapacity;

		public unmanaged_memory_append_only_list_should(int maxCapacity) {
			_maxCapacity = maxCapacity;
		}

		[SetUp]
		public void SetUp() {
			_list = new UnmanagedMemoryAppendOnlyList<int>(_maxCapacity);
		}

		[TearDown]
		public void TearDown() {
			_list?.Dispose();
		}

		[Test]
		public void correctly_add_items() {
			Assert.AreEqual(0, _list.Count);

			for (int i = 1; i <= _maxCapacity; i++) {
				_list.Add(i);
				Assert.AreEqual(i, _list.Count);
			}

			for (int i = 0; i < _maxCapacity; i++) {
				Assert.AreEqual(i + 1, _list[i]);
			}
		}

		[Test]
		public void correctly_clear_items() {
			Assert.AreEqual(0, _list.Count);

			//add _maxCapacity items with values 1.._maxCapacity
			for (int i = 1; i <= _maxCapacity; i++) {
				_list.Add(i);
				Assert.AreEqual(i, _list.Count);
			}

			//clear the list
			_list.Clear();
			Assert.AreEqual(0, _list.Count);

			//we should not be able to access any item at indexes 0.._maxCapacity-1
			for (int i = 0; i < _maxCapacity; i++) {
				Assert.Throws<IndexOutOfRangeException>(() => {
					var unused = _list[i];
				});
			}

			//add _maxCapacity items with values -1..-_maxCapacity
			for (int i = 1; i <= _maxCapacity; i++) {
				_list.Add(-i);
				Assert.AreEqual(i, _list.Count);
			}

			//verify that the new items have been correctly added
			for (int i = 0; i < _maxCapacity; i++) {
				Assert.AreEqual(-(i + 1), _list[i]);
			}
		}

		[Test]
		public void throw_max_capacity_reached_exception_if_list_is_full() {
			for (int i = 1; i <= _maxCapacity; i++) {
				_list.Add(0);
			}

			Assert.Throws<MaxCapacityReachedException>(() => {
				_list.Add(0);
			});
		}

		[Test]
		public void throw_index_out_of_range_exception_when_accessing_out_of_bounds_index() {
			int unused;
			Assert.Throws<IndexOutOfRangeException>(() => {
				unused = _list[-1];
			});

			Assert.Throws<IndexOutOfRangeException>(() => {
				unused = _list[-2];
			});

			for (int i = 0; i < _maxCapacity; i++) {
				Assert.Throws<IndexOutOfRangeException>(() => {
					unused = _list[i];
				});
				_list.Add(0);

				Assert.DoesNotThrow(() => {
					unused = _list[i];
				});
			}

			Assert.Throws<IndexOutOfRangeException>(() => {
				unused = _list[_maxCapacity];
			});

			Assert.Throws<IndexOutOfRangeException>(() => {
				unused = _list[_maxCapacity + 1];
			});
		}
	}

	public class unmanaged_memory_append_only_list_with_non_positive_capacity_should {
		[Test]
		public void throw_argument_exception() {
			Assert.Throws<ArgumentException>(() => {
				var unused = new UnmanagedMemoryAppendOnlyList<int>(0);
			});

			Assert.Throws<ArgumentException>(() => {
				var unused = new UnmanagedMemoryAppendOnlyList<int>(-1);
			});

			Assert.Throws<ArgumentException>(() => {
				var unused = new UnmanagedMemoryAppendOnlyList<int>(-2);
			});
		}
	}
}
