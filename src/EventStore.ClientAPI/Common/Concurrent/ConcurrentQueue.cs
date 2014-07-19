namespace EventStore.ClientAPI.Common.Concurrent
{
#if !PSEUDO_CONCURRENT_COLLECTIONS

    using System.Collections.Generic;

    internal class ConcurrentQueue<T> : System.Collections.Concurrent.ConcurrentQueue<T>
    {
        public ConcurrentQueue()
        {
        }

        public ConcurrentQueue(IEnumerable<T> items)
            : base(items)
        {
        }

        // JUST INHERITING EVERYTHING
    }

#else
    using EventStore.Common.Locks;
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using EventStore.Common.Utils;

    /// <summary>
    /// This is a not concurrent ConcurrentQueue that actually works with mono. Alas one day it may be fixed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ConcurrentQueue<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        private readonly Queue<T> _queue = new Queue<T>();
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        private SpinLock2 _spinLock = new SpinLock2();
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        public ConcurrentQueue()
        {

        }

        public ConcurrentQueue(IEnumerable<T> items)
        {
            Ensure.NotNull(items, "items");
            foreach (var item in items)
            {
                TryAdd(item);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _queue.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get
            {
                using (_spinLock.Acquire())
                {
                    return _queue.Count;
                }
            }
        }

        public object SyncRoot
        {
            get { return null; }
        }

        public bool TryTake(out T item)
        {
            item = default(T);
            using (_spinLock.Acquire())
            {
                if (_queue.Count == 0)
                    return false;
                item = _queue.Dequeue();
                return true;
            }
        }

        public bool TryPeek(out T item)
        {
            item = default(T);
            using (_spinLock.Acquire())
            {
                if (_queue.Count == 0)
                    return false;
                item = _queue.Peek();
                return true;
            }
        }

        public bool IsSynch ronized
        {
            get { return false; }
        }

        public void CopyTo(T[] array, int index)
        {
            using (_spinLock.Acquire())
            {
                _queue.CopyTo(array, index);
            }
        }

        public bool TryAdd(T item)
        {
            using (_spinLock.Acquire())
            {
                _queue.Enqueue(item);
                return true;
            }
        }

        public void Enqueue(T item)
        {
            TryAdd(item);
        }


        public bool TryDequeue(out T item)
        {
            return TryTake(out item);
        }

        public T[] ToArray()
        {
            using (_spinLock.Acquire())
                return _queue.ToArray();
        }
    }
}
#endif
}
