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

        public bool IsSynchronized
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
