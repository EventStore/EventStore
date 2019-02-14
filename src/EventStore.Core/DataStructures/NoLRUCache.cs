using System;

namespace EventStore.Core.DataStructures {
	public class NoLRUCache<TKey, TValue> : ILRUCache<TKey, TValue> {
		public void Clear() {
		}

		public bool TryGet(TKey key, out TValue value) {
			value = default(TValue);
			return false;
		}

		public TValue Put(TKey key, TValue value) {
			return value;
		}

		public TValue Put<T>(TKey key, T userData, Func<TKey, T, TValue> addFactory,
			Func<TKey, TValue, T, TValue> updateFactory) {
			return addFactory(key, userData);
		}
	}
}
