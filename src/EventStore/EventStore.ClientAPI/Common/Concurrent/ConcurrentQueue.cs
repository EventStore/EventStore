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

/*
Quote from

Professional .NET Framework 2.0 (Programmer to Programmer) (Paperback)
by Joe Duffy (Author)


# Paperback: 601 pages
# Publisher: Wrox (April 10, 2006)
# Language: English
# ISBN-10: 0764571354
# ISBN-13: 978-0764571350

*/

namespace EventStore.ClientAPI.Common.Concurrent
{
#if !PSEUDO_CONCURRENT_COLLECTIONS

    using System.Collections.Generic;

    public class ConcurrentQueue<T> : System.Collections.Concurrent.ConcurrentQueue<T>
    {
        public ConcurrentQueue()
        {
        }

        public ConcurrentQueue(IEnumerable<T> items): base(items)
        {
        }

        // JUST INHERITING EVERYTHING
    }

#else

    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using EventStore.Common.Concurrent;
    using EventStore.ClientAPI.Common.Utils;
    
    /// <summary>
    /// This is a not concurrent ConcurrentQueue that actually works with mono. Alas one day it may be fixed.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConcurrentQueue<T>: IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        private readonly Queue<T> _queue = new Queue<T>();
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        private SpinLock2 _padLock = new SpinLock2();
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
                bool gotLock = false;
                try
                {
                    _padLock.Enter(out gotLock);
                    return _queue.Count;
                }
                finally
                {
                    if (gotLock) _padLock.Exit();
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
            bool gotLock = false;
            try
            {
                _padLock.Enter(out gotLock);
                if (_queue.Count == 0)
                    return false;
                item = _queue.Dequeue();
                return true;
            }
            finally
            {
                if (gotLock) _padLock.Exit();
            }
        }

        public bool TryPeek(out T item)
        {
            item = default(T);
            bool gotLock = false;
            try
            {
                _padLock.Enter(out gotLock);
                if (_queue.Count == 0) 
                    return false;
                item = _queue.Peek();
                return true;
            }
            finally
            {
                if (gotLock) _padLock.Exit();
            }
        }

        public bool IsSynchronized
        {
            get { return false; }
        }

        public void CopyTo(T[] array, int index)
        {
            bool gotLock = false;
            try
            {
                _padLock.Enter(out gotLock);
                _queue.CopyTo(array, index);
            }
            finally
            {
                if (gotLock) _padLock.Exit();
            }
        }

        public bool TryAdd(T item)
        {
            bool gotLock = false;
            try
            {
                _padLock.Enter(out gotLock);
                _queue.Enqueue(item);
                return true;
            }
            finally
            {
                if (gotLock) _padLock.Exit();
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
            bool gotLock = false;
            try
            {
                _padLock.Enter(out gotLock);
                return _queue.ToArray();
            }
            finally
            {
                if (gotLock) _padLock.Exit();
            }
        }
    }
#endif
}
