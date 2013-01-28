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
using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.Core.DataStructures
{
    public class ObjectPoolDisposingException : Exception
    {
        public ObjectPoolDisposingException(string poolName)
            : this(poolName, null)
        {
        }

        public ObjectPoolDisposingException(string poolName, Exception innerException)
            : base(string.Format("Object pool '{0}' is disposing/disposed while Get operation is requested.", poolName), innerException)
        {
            Ensure.NotNullOrEmpty(poolName, "poolName");
        }
    }

    public class ObjectPoolMaxLimitReachedException : Exception
    {
        public ObjectPoolMaxLimitReachedException(string poolName, int maxLimit)
            : this(poolName, maxLimit, null)
        {
        }

        public ObjectPoolMaxLimitReachedException(string poolName, int maxLimit, Exception innerException)
            : base(string.Format("Object pool '{0}' has reached its max limit for items: {1}.", poolName, maxLimit), innerException)
        {
            Ensure.NotNullOrEmpty(poolName, "poolName");
            Ensure.Nonnegative(maxLimit, "maxLimit");
        }
    }

    public class ObjectPool<T> : IDisposable
    {
        public readonly string ObjectPoolName;

        private readonly Common.Concurrent.ConcurrentQueue<T> _queue = new Common.Concurrent.ConcurrentQueue<T>();
        private readonly int _maxCount;
        private readonly Func<T> _factory;
        private readonly Action<T> _dispose;
        private readonly Action<ObjectPool<T>> _onPoolDisposed;

        private int _count;
        private volatile bool _disposing;
        private int _poolDisposed;

        public ObjectPool(string objectPoolName,
                          int initialCount,
                          int maxCount,
                          Func<T> factory,
                          Action<T> dispose = null,
                          Action<ObjectPool<T>> onPoolDisposed = null)
        {
            Ensure.NotNullOrEmpty(objectPoolName, "objectPoolName");
            Ensure.Nonnegative(initialCount, "initialCount");
            Ensure.Nonnegative(maxCount, "maxCount");
            if (initialCount > maxCount)
                throw new ArgumentOutOfRangeException("initialCount", "initialCount is greater than maxCount.");
            Ensure.NotNull(factory, "factory");

            ObjectPoolName = objectPoolName;
            _maxCount = maxCount;
            _count = initialCount;
            _factory = factory;
            _dispose = dispose ?? (x => { });
            _onPoolDisposed = onPoolDisposed ?? (x => { });

            for (int i = 0; i < initialCount; ++i)
            {
                _queue.Enqueue(factory());
            }
        }

        public void MarkForDisposal()
        {
            Dispose();
        }

        public void Dispose()
        {
            _disposing = true;
            TryDestruct();
        }

        public T Get()
        {
            if (_disposing)
                throw new ObjectPoolDisposingException(ObjectPoolName);

            T item;
            if (_queue.TryDequeue(out item))
                return item;

            var newCount = Interlocked.Increment(ref _count);
            if (newCount > _maxCount)
                throw new ObjectPoolMaxLimitReachedException(ObjectPoolName, _maxCount);

            if (_disposing)
            {
                if (Interlocked.Decrement(ref _count) == 0)
                    OnPoolDisposed(); // now we possibly should "turn light off"
                throw new ObjectPoolDisposingException(ObjectPoolName);
            }

            // if we get here, then it is safe to return newly created item to user, object pool won't be disposed 
            // until that item is returned to pool
            return _factory();
        }

        public void Return(T item)
        {
            _queue.Enqueue(item);
            if (_disposing)
                TryDestruct();
        }

        private void TryDestruct()
        {
            int count = int.MaxValue;

            T item;
            while (_queue.TryDequeue(out item))
            {
                _dispose(item);
                count = Interlocked.Decrement(ref _count);
            }

            if (count < 0)
                throw new Exception("Somehow we managed to decrease count of pool items below zero.");
            if (count == 0) // we are the last who should "turn the light off" 
                OnPoolDisposed();
        }

        private void OnPoolDisposed()
        {
            // ensure that we call _onPoolDisposed just once
            if (Interlocked.CompareExchange(ref _poolDisposed, 1, 0) == 0)
                _onPoolDisposed(this);
        }
    }
}
