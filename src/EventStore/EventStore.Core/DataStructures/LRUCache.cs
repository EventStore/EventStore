using System;
using System.Collections.Generic;

namespace EventStore.Core.DataStructures
{
    public class LRUCache<TKey, TValue>: ILRUCache<TKey, TValue>
    {
        private class LRUItem
        {
            public TKey Key;
            public TValue Value;
        }

        private readonly LinkedList<LRUItem> _orderList = new LinkedList<LRUItem>();
        private readonly Dictionary<TKey, LinkedListNode<LRUItem>> _items = new Dictionary<TKey, LinkedListNode<LRUItem>>();
        private readonly Queue<LinkedListNode<LRUItem>> _nodesPool = new Queue<LinkedListNode<LRUItem>>();

        private readonly int _maxCount;
        private readonly object _lock = new object();

        public LRUCache(int maxCount)
        {
            if (maxCount <= 0)
                throw new ArgumentOutOfRangeException("maxCount");

            _maxCount = maxCount;
        }

        public bool TryGet(TKey key, out TValue value)
        {
            lock (_lock)
            {
                LinkedListNode<LRUItem> node;
                if (_items.TryGetValue(key, out node))
                {
                    _orderList.Remove(node);
                    _orderList.AddLast(node);
                    value = node.Value.Value;
                    return true;
                }

                value = default(TValue);
                return false;
            }
        }

        public void Put(TKey key, TValue value)
        {
            lock (_lock)
            {
                LinkedListNode<LRUItem> node;
                if (!_items.TryGetValue(key, out node))
                {
                    node = GetNode();
                    node.Value.Key = key;
                    node.Value.Value = value;

                    EnsureCapacity();

                    _items.Add(key, node);
                }
                else
                {
                    node.Value.Value = value;
                    _orderList.Remove(node);
                }
                _orderList.AddLast(node);
            }
        }
        
        private void EnsureCapacity()
        {
            while (_items.Count >= _maxCount)
            {
                var node = _orderList.First;
                _orderList.Remove(node);
                _items.Remove(node.Value.Key);

                ReturnNode(node);
            }
        }

        private LinkedListNode<LRUItem> GetNode()
        {
            if (_nodesPool.Count > 0)
                return _nodesPool.Dequeue();
            return new LinkedListNode<LRUItem>(new LRUItem());
        }

        private void ReturnNode(LinkedListNode<LRUItem> node)
        {
            _nodesPool.Enqueue(node);
        }
    }
}
