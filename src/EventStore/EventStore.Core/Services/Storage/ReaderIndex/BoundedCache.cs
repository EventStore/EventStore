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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.DataStructures;

namespace EventStore.Core.Services.Storage.ReaderIndex
{
    public class BoundedCache<TKey, TValue>
    {
        public int Count { get { return _cache.Count; } }

        private readonly int _maxCachedEntries;
        private readonly long _maxDataSize;
        private readonly Func<TValue, long> _valueSize;

        private readonly Dictionary<TKey, TValue> _cache = new Dictionary<TKey, TValue>();
        private readonly Queue<TKey> _queue = new Queue<TKey>();

        private long _currentSize;
        private long _missCount;
        private long _hitCount;

        public BoundedCache(int maxCachedEntries, long maxDataSize, Func<TValue, long> valueSize)
        {
            Ensure.NotNull(valueSize, "valueSize");
            if (maxCachedEntries <= 0)
                throw new ArgumentOutOfRangeException("maxCachedEntries");
            if (maxDataSize <= 0)
                throw new ArgumentOutOfRangeException("maxDataSize");

            _maxCachedEntries = maxCachedEntries;
            _maxDataSize = maxDataSize;
            _valueSize = valueSize;
        }

        public bool TryGetRecord(TKey key, out TValue value)
        {
            var found = _cache.TryGetValue(key, out value);
            if (found)
                _hitCount++;
            else
                _missCount++;
            return found;
        }

        public void PutRecord(TKey key, TValue value)
        {
            PutRecord(key, value, true);
        }

        public void PutRecord(TKey key, TValue value, bool throwOnDuplicate)
        {
            while (IsFull())
            {
                var oldKey = _queue.Dequeue();
                RemoveRecord(oldKey);
            }
            _currentSize += _valueSize(value);
            _queue.Enqueue(key);
            if (!throwOnDuplicate && _cache.ContainsKey(key))
                return;
            _cache.Add(key, value);
        }

        public bool TryPutRecord(TKey key, TValue value)
        {
            if (IsFull())
                return false;

            _currentSize += _valueSize(value);
            _queue.Enqueue(key);
            _cache.Add(key, value); // add to throw exception if duplicate
            return true;
        }

        public void Clear()
        {
            _currentSize = 0;
            _queue.Clear();
            _cache.Clear();
        }

        private bool IsFull()
        {
            return _queue.Count >= _maxCachedEntries 
                   || (_currentSize > _maxDataSize && _queue.Count > 0);
        }

        public void RemoveRecord(TKey key)
        {
            TValue old;
            if (_cache.TryGetValue(key, out old))
            {
                _currentSize -= _valueSize(old);
                _cache.Remove(key);
            }
        }

        public ReadCacheStats GetStatistics()
        {
            return new ReadCacheStats(Interlocked.Read(ref _currentSize),
                                      _cache.Count,
                                      Interlocked.Read(ref _hitCount),
                                      Interlocked.Read(ref _missCount));
        }
    }
}