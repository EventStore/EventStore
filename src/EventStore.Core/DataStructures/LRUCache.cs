using System;
using System.Collections.Generic;
using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.Core.DataStructures {
	public class LRUCache<TKey, TValue> : ILRUCache<TKey, TValue> {
		private class LRUItem {
			public TKey Key;
			public TValue Value;
		}

		private readonly LinkedList<LRUItem> _orderList = new();
		private readonly Dictionary<TKey, LinkedListNode<LRUItem>> _items = new();
		private readonly Queue<LinkedListNode<LRUItem>> _nodesPool = new();
		private readonly object _lock = new();
		private readonly Func<object, bool> _onPut, _onRemove; //_onPut is not called if a key-value pair already exists in the cache
		protected long _capacity; //qq prefer private field
		private long _size;
		protected readonly Func<TKey, TValue, int> _calculateItemSize;
		private static readonly Func<TKey, TValue, int> _unitSize = (_, _) => 1;

		public long Size => Interlocked.Read(ref _size);
		public long Capacity => Interlocked.Read(ref _capacity);

		public LRUCache(
			long capacity, //qq in bytes
			Func<TKey, TValue, int> calculateItemSize = null) {
			Ensure.Nonnegative(capacity, nameof(capacity));
			_capacity = capacity;
			_size = 0L;
			_calculateItemSize = calculateItemSize ?? _unitSize;
		}

		public LRUCache(
			long capacity,
			Func<object, bool> onPut,
			Func<object, bool> onRemove,
			Func<TKey, TValue, int> calculateItemSize = null) {
			Ensure.Nonnegative(capacity, "capacity");
			_capacity = capacity;
			_size = 0L;
			_onPut = onPut;
			_onRemove = onRemove;
			_calculateItemSize = calculateItemSize ?? _unitSize;
		}

		private void PutItem(TKey key, TValue value) {
			lock (_lock) {
				var node = GetNode();
				node.Value.Key = key;
				node.Value.Value = value;

				var itemSize = _calculateItemSize(key, value);
				EnsureCapacity(itemSize, out _, out _);

				_items.Add(key, node);
				_orderList.AddLast(node);
				_size += itemSize;

				_onPut?.Invoke(node.Value.Value);
			}
		}

		private void UpdateItem(LinkedListNode<LRUItem> node, TValue value) {
			lock (_lock) {
				_size -= _calculateItemSize(node.Value.Key, node.Value.Value);
				_size += _calculateItemSize(node.Value.Key, value);
				node.Value.Value = value;

				if (!ReferenceEquals(node, _orderList.Last)) {
					_orderList.Remove(node);
					_orderList.AddLast(node);
				}

				if (_size > _capacity)
					EnsureCapacity(0, out _, out _);
			}
		}

		private void RemoveItem(TKey key) {
			lock (_lock) {
				LinkedListNode<LRUItem> node;
				if (_items.TryGetValue(key, out node)) {
					_orderList.Remove(node);
					_items.Remove(key);
					_size -= _calculateItemSize(key, node.Value.Value);

					var value = node.Value.Value;
					ReturnNode(node);

					_onRemove?.Invoke(value);
				}
			}
		}

		private void RemoveFirstItem() {
			lock (_lock) {
				var node = _orderList.First;
				if (node == null)
					return;

				_orderList.Remove(node);
				_items.Remove(node.Value.Key);
				_size -= _calculateItemSize(node.Value.Key, node.Value.Value);

				var value = node.Value.Value;
				ReturnNode(node);

				_onRemove?.Invoke(value);
			}
		}

		public bool TryGet(TKey key, out TValue value) {
			lock (_lock) {
				LinkedListNode<LRUItem> node;
				if (_items.TryGetValue(key, out node)) {
					_orderList.Remove(node);
					_orderList.AddLast(node);
					value = node.Value.Value;
					return true;
				}

				value = default;
				return false;
			}
		}

		public TValue Put(TKey key, TValue value) {
			lock (_lock) {
				LinkedListNode<LRUItem> node;
				if (!_items.TryGetValue(key, out node))
					PutItem(key, value);
				else
					UpdateItem(node, value);

				return value;
			}
		}

		public void Remove(TKey key) {
			lock (_lock) {
				RemoveItem(key);
			}
		}

		public void Clear() {
			lock (_lock) {
				while (_orderList.Count > 0)
					RemoveFirstItem();
			}
		}

		public TValue Put<T>(TKey key, T userData, Func<TKey, T, TValue> addFactory,
			Func<TKey, TValue, T, TValue> updateFactory) {
			lock (_lock) {
				TValue value;
				LinkedListNode<LRUItem> node;
				if (!_items.TryGetValue(key, out node)) {
					value = addFactory(key, userData);
					PutItem(key, value);
				} else {
					value = updateFactory(key, node.Value.Value, userData);
					UpdateItem(node, value);
				}

				return value;
			}
		}

		protected void EnsureCapacity(int forItemSize, out int removedCount, out long removedSize) {
			lock (_lock) {
				var initialCount = _items.Count;
				var initialSize = _size;

				while (_items.Count > 0 && _size + forItemSize > _capacity)
					RemoveFirstItem();

				removedCount = initialCount - _items.Count;
				removedSize = initialSize - _size;
			}
		}

		private LinkedListNode<LRUItem> GetNode() {
			if (_nodesPool.Count > 0)
				return _nodesPool.Dequeue();
			return new LinkedListNode<LRUItem>(new LRUItem());
		}

		private void ReturnNode(LinkedListNode<LRUItem> node) {
			_nodesPool.Enqueue(node);
		}
	}
}
