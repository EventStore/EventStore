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
#define USE_POOL

using System;
using System.Collections.Generic;

namespace EventStore.Core.DataStructures
{

    public class PairingHeap<T>
    {
#if USE_POOL
        private static readonly ObjectPool<HeapNode> NodePool = new ObjectPool<HeapNode>(50000, () => new HeapNode());
#endif
        public int Count { get { return _count; } }

        private HeapNode _root;
        private int _count;
        private readonly Func<T, T, bool> _compare;

        public PairingHeap(): this(null, null as IComparer<T>)
        {
        }

        public PairingHeap(IComparer<T> comparer): this(null, comparer)
        {
            if (comparer == null)
                throw new ArgumentNullException("comparer");
        }

        public PairingHeap(Func<T, T, bool> compare): this(null, compare)
        {
            if (compare == null)
                throw new ArgumentNullException("compare");
        }

        public PairingHeap(IEnumerable<T> items): this(items, null as IComparer<T>)
        {
        }

        public PairingHeap(IEnumerable<T> items, Func<T, T, bool> compare)
        {
            if (compare == null)
            {
                var comparer = Comparer<T>.Default;
                _compare = (x, y) => comparer.Compare(x, y) < 0;
            }
            else
            {
                _compare = compare;
            }

            if (items != null)
            {
                foreach (var item in items)
                {
                    Add(item);
                }
            }
        }

        public PairingHeap(IEnumerable<T> items, IComparer<T> comparer)
        {
            var comp = comparer ?? Comparer<T>.Default;
            _compare = (x, y) => comp.Compare(x, y) < 0;

            if (items != null)
            {
                foreach (var item in items)
                {
                    Add(item);
                }
            }
        }

        public void Add(T x)
        {
#if USE_POOL
            var newNode = NodePool.Get();
#else
            var newNode = new HeapNode();
#endif
            newNode.Item = x;
            _root = Meld(_root, newNode);
            _count += 1;
        }

        public T FindMin()
        {
            if (Count == 0)
                throw new InvalidOperationException();
            return _root.Item;
        }

        public T DeleteMin()
        {
            if (Count == 0)
                throw new InvalidOperationException();

            var oldRoot = _root;
            var res = _root.Item;
            _root = Pair(_root.SubHeaps);
            _count -= 1;
#if USE_POOL
            oldRoot.Next = null;
            oldRoot.SubHeaps = null;
            NodePool.Return(oldRoot);
#endif
            return res;
        }

        private HeapNode Meld(HeapNode heap1, HeapNode heap2)
        {
            if (heap1 == null)
                return heap2;
            if (heap2 == null)
                return heap1;

            if (_compare(heap1.Item, heap2.Item))
            {
                heap2.Next = heap1.SubHeaps;
                heap1.SubHeaps = heap2;
                return heap1;
            }
            else
            {
                heap1.Next = heap2.SubHeaps;
                heap2.SubHeaps = heap1;
                return heap2;
            }
        }

        private HeapNode Pair(HeapNode node)
        {
            HeapNode tail = null;
            HeapNode cur = node;

            while (cur != null && cur.Next != null)
            {
                var n1 = cur;
                var n2 = cur.Next;
                cur = cur.Next.Next;

                n1.Next = tail;
                n2.Next = n1;
                tail = n2;
            }

            while (tail != null)
            {
                var n = tail;
                tail = tail.Next.Next;
                cur = Meld(cur, Meld(n, n.Next));
            }

            return cur;
        }

        private class HeapNode
        {
            public T Item;
            public HeapNode SubHeaps;
            public HeapNode Next;
        }

#if USE_POOL
        private class ObjectPool<TItem> where TItem : class
        {
            private readonly Common.Concurrent.ConcurrentQueue<TItem> _items = new Common.Concurrent.ConcurrentQueue<TItem>();

            private readonly int _count;
            private readonly Func<TItem> _creator;

            public ObjectPool(int count, Func<TItem> creator)
            {
                if (count < 0)
                    throw new ArgumentOutOfRangeException();
                if (creator == null)
                    throw new ArgumentNullException("creator");

                _count = count;
                _creator = creator;

                for (int i = 0; i < count; ++i)
                {
                    _items.Enqueue(creator());
                }
            }

            public TItem Get()
            {
                TItem res;
                if (_items.TryDequeue(out res))
                    return res;
                return _creator();
            }

            public void Return(TItem item)
            {
                if (_items.Count < _count)
                    _items.Enqueue(item);
            }
        }
#endif
    
    }
}
