using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace EventStore.Common.ConcurrentCollections
{
    /// <summary>
    /// Not very concurrent stack for use in mono
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConcurrentStack<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        private readonly Stack<T> _stack = new Stack<T>();
        private readonly object _padLock = new object();

        public IEnumerator<T> GetEnumerator()
        {
            return _stack.GetEnumerator();
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
                    return _stack.Count;
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
                _stack.CopyTo(array, index);
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
                _stack.Push(item);
                return true;
            }
            finally
            {
                Monitor.Exit(_padLock);
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

        public bool TryTake(out T item)
        {
            item = default(T);
            if (!Monitor.TryEnter(_padLock, 5000)) return false;
            try
            {
                item = _stack.Pop();
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
                return _stack.ToArray();
            }
            finally
            {
                Monitor.Exit(_padLock);
            }
        }
    }
}
