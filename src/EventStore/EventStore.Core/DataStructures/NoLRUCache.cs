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

        public void Put(TKey key, TValue value)
        {
        }

        public void Put(TKey key, Func<TKey, TValue> addFactory, Func<TKey, TValue, TValue> updateFactory)
        {
        }
    }
}