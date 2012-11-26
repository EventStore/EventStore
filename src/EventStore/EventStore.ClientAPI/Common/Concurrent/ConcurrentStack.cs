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

    public class ConcurrentStack<T> : System.Collections.Concurrent.ConcurrentStack<T>
    {
        // JUST INHERITING EVERYTHING
    }

#else

    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using EventStore.Common.Concurrent;

    /// <summary>
    /// Not very concurrent stack for use in mono
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConcurrentStack<T> : IProducerConsumerCollection<T>, IEnumerable<T>, ICollection, IEnumerable
    {
        private readonly Stack<T> _stack = new Stack<T>();
        // ReSharper disable FieldCanBeMadeReadOnly.Local
        private SpinLock2 _padLock = new SpinLock2();
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
                bool gotLock = false;
                try
                {
                    _padLock.Enter(out gotLock);
                    return _stack.Count;
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

        public bool IsSynchronized
        {
            get { return true; }
        }

        public void CopyTo(T[] array, int index)
        {
            bool gotLock = false;
            try
            {
                _padLock.Enter(out gotLock);
                _stack.CopyTo(array, index);
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
                _stack.Push(item);
                return true;
            }
            finally
            {
                if (gotLock) _padLock.Exit();
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
            bool gotLock = false;
            try
            {
                _padLock.Enter(out gotLock);
                if (_stack.Count == 0) 
                    return false;
                item = _stack.Pop();
                return true;
            }
            finally
            {
                if (gotLock) _padLock.Exit();
            }
        }

        public T[] ToArray()
        {
            bool gotLock = false;
            try
            {
                _padLock.Enter(out gotLock);
                return _stack.ToArray();
            }
            finally
            {
                if (gotLock) _padLock.Exit();
            }
        }
    }
#endif
}
