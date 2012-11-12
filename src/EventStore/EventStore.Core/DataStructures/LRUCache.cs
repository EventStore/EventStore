// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  
using System;
using System.Collections.Generic;
using EventStore.Common.Utils;

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

        public void Put(TKey key, Func<TKey, TValue> addFactory, Func<TKey, TValue, TValue> updateFactory)
        {
            Ensure.NotNull(addFactory, "addFactory");
            Ensure.NotNull(updateFactory, "updateFactory");

            lock (_lock)
            {
                LinkedListNode<LRUItem> node;
                if (!_items.TryGetValue(key, out node))
                {
                    node = GetNode();
                    node.Value.Key = key;
                    node.Value.Value = addFactory(key);

                    EnsureCapacity();

                    _items.Add(key, node);
                }
                else
                {
                    node.Value.Value = updateFactory(key, node.Value.Value);
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
