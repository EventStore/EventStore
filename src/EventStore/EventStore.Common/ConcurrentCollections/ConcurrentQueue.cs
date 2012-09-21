using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace EventStore.Common.ConcurrentCollections
{
    /// <summary>
    /// This is a not concurrent concurrentqueue that actually works with mono. Alas one day it may be fixed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConcurrentQueue<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly object _padLock = new object();

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
                if (!Monitor.TryEnter(_padLock, 5000)) throw new UnableToAcquireLockException();
                try
                {
                    return _queue.Count;
                }
                finally
                {
                    Monitor.Exit(_padLock);
                }
            }
        }

        public object SyncRoot
        {
            get { return _padLock; }
        }

        public bool IsSynchronized
        {
            get { return true; }
        }

        public void CopyTo(T[] array, int index)
        {
            if (!Monitor.TryEnter(_padLock, 5000)) throw new UnableToAcquireLockException();
            try
            {
                _queue.CopyTo(array, index);
            }
            finally
            {
                Monitor.Exit(_padLock);
            }
        }

        public bool TryAdd(T item)
        {
            if (!Monitor.TryEnter(_padLock, 5000)) return false;
            try
            {
                _queue.Enqueue(item);
                return true;
            }
            finally
            {
                Monitor.Exit(_padLock);
            }
        }

        public bool TryTake(out T item)
        {
            item = default(T);
            if (!Monitor.TryEnter(_padLock, 5000)) return false;
            try
            {
                item = _queue.Dequeue();
                return true;
            }
            finally
            {
                Monitor.Exit(_padLock);
            }
        }

        public T[] ToArray()
        {
            if (!Monitor.TryEnter(_padLock, 5000)) throw new UnableToAcquireLockException();
            try
            {
                return _queue.ToArray();
            }
            finally
            {
                Monitor.Exit(_padLock);
            }
        }
    }

    public class UnableToAcquireLockException : Exception
    {
    }

}
