using System;

namespace EventStore.Core.DataStructures
{
    public class NoLRUCache<TKey, TValue>: ILRUCache<TKey, TValue>
    {
        public bool TryGet(TKey key, out TValue value)
        {
            value = default(TValue);
            return false;
        }

        public TValue Put(TKey key, TValue value)
        {
            return value;
        }

        public TValue Put(TKey key, Func<TKey, TValue> addFactory, Func<TKey, TValue, TValue> updateFactory)
        {
            return addFactory(key);
        }
    }
}