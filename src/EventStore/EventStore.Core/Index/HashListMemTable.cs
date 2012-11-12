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
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Core.Exceptions;

namespace EventStore.Core.Index
{
    public class HashListMemTable : IMemTable, ISearchTable
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<HashListMemTable>();
        private static readonly IComparer<Tuple<int, long>> MemTableComparer = new MemTableComparer();

        public int Count { get { return _count; } }
        public Guid Id { get { return _id; } }
        public long LatestPosition { get { return _latestPosition; } }

        private readonly Dictionary<uint, SortedList<Tuple<int, long>, byte>> _hash;
        private readonly Guid _id = Guid.NewGuid();
        private int _count;
        private long _latestPosition;

        private int _isConverting;

        public HashListMemTable(int maxSize = 2000000)
        {
            _hash = new Dictionary<uint, SortedList<Tuple<int, long>, byte>>(maxSize);
        }

        public bool MarkForConversion()
        {
            return Interlocked.CompareExchange(ref _isConverting, 1, 0) == 0;
        }

        public void Add(uint stream, int version, long position)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException("version");
            if (position < 0)
                throw new ArgumentOutOfRangeException("position");

            //only one thread at a time can write
            Interlocked.Increment(ref _count);

            SortedList<Tuple<int, long>, byte> list;
            if (!_hash.TryGetValue(stream, out list))
            {
                list = new SortedList<Tuple<int, long>, byte>(MemTableComparer);
                _hash.Add(stream, list);
            }

            if (!Monitor.TryEnter(list, 10000))
                throw new UnableToAcquireLockInReasonableTimeException();
            try
            {
                var tuple = new Tuple<int, long>(version, position);
                // TODO AN: why do we need to check for existing value?..
                //if (!list.ContainsKey(tuple))
                {
                    list.Add(tuple, 1);

                    // TODO AN: positions should be strictly increasing, no need for Max
                    //_latestPosition = Math.Max(_latestPosition, position);
                    _latestPosition = position;
                }
            }
            finally
            {
                Monitor.Exit(list);
            }

        }

        public bool TryGetOneValue(uint stream, int version, out long position)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException("version");

            position = 0;

            SortedList<Tuple<int, long>, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000)) throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    int endIdx = list.UpperBound(Tuple.Create(version, long.MaxValue));
                    if (endIdx == -1)
                        return false;

                    var key = list.Keys[endIdx];
                    if (key.Item1 == version)
                    {
                        position = key.Item2;
                        return true;
                    }
                }
                finally
                {
                    Monitor.Exit(list);
                }
            }
            return false;
        }

        public bool TryGetLatestEntry(uint stream, out IndexEntry entry)
        {
            entry = TableIndex.InvalidIndexEntry;

            SortedList<Tuple<int, long>, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000))
                    throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    var latest = list.Keys[list.Count - 1];
                    entry = new IndexEntry(stream, latest.Item1, latest.Item2);
                    return true;
                }
                finally
                {
                    Monitor.Exit(list);
                }
            }
            return false;
        }

        public IEnumerable<IndexEntry> IterateAllInOrder()
        {
            Log.Trace("Sorting array in HashListMemTable.IterateAllInOrder...");

            var items = _hash.Keys.ToArray();
            Array.Sort(items, new ReverseComparer<uint>());
            
            foreach (var item in items)
            {
                var hash = _hash[item];
                for (int i = hash.Count - 1; i >= 0; --i)
                {
                    var x = hash.Keys[i];
                    yield return new IndexEntry(item, x.Item1, x.Item2);
                }
            }
            Log.Trace("Sorting array in HashListMemTable.IterateAllInOrder... DONE!");
        }

        public void Clear()
        {
            _hash.Clear();
        }

        public IEnumerable<IndexEntry> GetRange(uint stream, int startVersion, int endVersion)
        {
            if (startVersion < 0)
                throw new ArgumentOutOfRangeException("startVersion");
            if (endVersion < 0)
                throw new ArgumentOutOfRangeException("endVersion");

            var ret = new List<IndexEntry>();

            SortedList<Tuple<int, long>, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000)) throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    var endIdx = list.UpperBound(Tuple.Create(endVersion, long.MaxValue));
                    for (int i = endIdx; i >= 0; i--)
                    {
                        var key = list.Keys[i];
                        if (key.Item1 < startVersion)
                            break;
                        ret.Add(new IndexEntry(stream, version: key.Item1, position: key.Item2));
                    }
                }
                finally
                {
                    Monitor.Exit(list);
                }
            }
            return ret;
        }
    }

    public class MemTableComparer : IComparer<Tuple<int, long>>
    {
        public int Compare(Tuple<int, long> x, Tuple<int, long> y)
        {
            var first = x.Item1.CompareTo(y.Item1);
            if (first != 0) return first;
            return x.Item2.CompareTo(y.Item2);
        }
    }

    public class ReverseComparer<T> : IComparer<T> where T : IComparable
    {
        public int Compare(T x, T y)
        {
            return -x.CompareTo(y);
        }
    }
}