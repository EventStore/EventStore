namespace EventStore.ClientAPI.Common.Concurrent
{
#if !PSEUDO_CONCURRENT_COLLECTIONS

    internal class ConcurrentStack<T> : System.Collections.Concurrent.ConcurrentStack<T>
    {
        // JUST INHERITING EVERYTHING
    }

#else

    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using EventStore.Common.Locks;
    /// <summary>
    /// Not very concurrent stack for use in mono
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ConcurrentStack<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        private readonly Stack<T> _stack = new Stack<T>();
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        private SpinLock2 _spinLock = new SpinLock2();
        // ReSharper restore FieldCanBeMadeReadOnly.Local

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
                using(_spinLock.Acquire()) {
                    return _stack.Count;
                }
            }
        }

        public object SyncRoot
        {
            get { return null; }
        }

        public bool IsSynchronized
        {
            get { return true; }
        }

        public void CopyTo(T[] array, int index)
        {
            using(_spinLock.Acquire()) {
                _stack.CopyTo(array, index);
            }
        }

        public bool TryAdd(T item)
        {
            using(_spinLock.Acquire()) {
                _stack.Push(item);
                return true;
            }
        }

        public void Push(T item)
        {
            TryAdd(item);
        }

        public bool TryPop(out T item)
        {
            return TryTake(out item);
        }

        public bool TryTake(out T item)
        {
            item = default(T);
            using (_spinLock.Acquire())
            {
                if (_stack.Count == 0)
                    return false;
                item = _stack.Pop();
                return true;
            }
        }

        public T[] ToArray()
        {
            using(_spinLock.Acquire()) {
                return _stack.ToArray();
            }
        }
    }
#endif
}
