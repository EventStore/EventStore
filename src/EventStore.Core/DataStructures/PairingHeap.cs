using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EventStore.Core.DataStructures
{

    public class PairingHeap<T>
    {
        public int Count { get { return _count; } }

        public interface IHeapNodeFactory
        {
            HeapNode Acquire();
            void Release(HeapNode oldNode);
        }

        private HeapNode _root;
        private int _count;
        private readonly Func<T, T, bool> _compare;
        private readonly IHeapNodeFactory _nodeFactory = new ObjectPoolNodeFactory(100);

        private class DefaultNodeFactory : IHeapNodeFactory
        {
            public HeapNode Acquire() { return new HeapNode(); }
            public void Release(HeapNode oldNode) { }
        }

        private class ObjectPoolNodeFactory : ObjectPool<HeapNode>, IHeapNodeFactory
        {
            public ObjectPoolNodeFactory(int initialCount)
                : base("PairingHeap", initialCount, int.MaxValue, () => new HeapNode(), null, null)
            {}

            public HeapNode Acquire()
            {
                return Get();
            }

            public void Release(HeapNode oldNode)
            {
                oldNode.Next = null;
                oldNode.SubHeaps = null;
                Return(oldNode);
            }
        }

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
            _compare = ConvertComparer(comparer);

            if (items != null)
            {
                foreach (var item in items)
                {
                    Add(item);
                }
            }
        }

        private static Func<T, T, bool> ConvertComparer(IComparer<T> comparer)
        {
            var comp = comparer ?? Comparer<T>.Default;
            return (x, y) => comp.Compare(x, y) < 0;
        }

        public void Add(T x)
        {
            var newNode = _nodeFactory.Acquire();
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

            _nodeFactory.Release(oldRoot);
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
    
            heap1.Next = heap2.SubHeaps;
            heap2.SubHeaps = heap1;
            return heap2;
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

        public class HeapNode
        {
            public T Item;
            public HeapNode SubHeaps;
            public HeapNode Next;
        }
    }
}
