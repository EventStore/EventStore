namespace EventStore.Core.DataStructures
{
    public interface ILRUCache<in TKey, TValue>
    {
        bool TryGet(TKey key, out TValue value);
        void Put(TKey key, TValue value);
    }

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
    }
}