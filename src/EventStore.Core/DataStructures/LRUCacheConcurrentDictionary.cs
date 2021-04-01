using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EventStore.Core.DataStructures {
	public class LRUCacheConcurrentDictionary<TKey, TValue> : ILRUCache<TKey, TValue> {
		private class LRUItem {
			public TKey Key;
			public TValue Value;
		}

		private readonly ConcurrentDictionary<TKey, LinkedListNode<LRUItem>> _items;

		public LRUCacheConcurrentDictionary(int concurrencyLevel, int initialCapacity) {
			_items = new ConcurrentDictionary<TKey, LinkedListNode<LRUItem>>(concurrencyLevel, initialCapacity);
		}

		public bool TryGet(TKey key, out TValue value) {
			if (_items.TryGetValue(key, out var node)) {
				value = node.Value.Value;
				return true;
			}

			value = default(TValue);
			return false;
		}

		public TValue Put(TKey key, TValue value) {
			var node = _items.GetOrAdd(key, k => {
				var n = GetNode();
				n.Value.Key = k;
				return n;
			});

			node.Value.Value = value;
			return value;
		}

		public void Remove(TKey key) {
			_items.Remove(key, out _);
		}

		public void Clear() {
			_items.Clear();
		}

		public TValue Put<T>(TKey key, T userData, Func<TKey, T, TValue> addFactory,
			Func<TKey, TValue, T, TValue> updateFactory) {

			// allocations :[
			var node = _items.AddOrUpdate(
				key,
				addValueFactory: (k, t) => {
					var n = GetNode();
					n.Value.Key = key;
					n.Value.Value = addFactory(k, t);
					return n;
				},
				updateValueFactory: (k, n, t) => {
					n.Value.Value = updateFactory(k, n.Value.Value, userData);
					return n;
				},
				userData);

			return node.Value.Value;
		}

		private LinkedListNode<LRUItem> GetNode() {
			return new LinkedListNode<LRUItem>(new LRUItem());
		}
	}
}
