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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;

namespace EventStore.Core.Index
{
    public class HashListMemTable : IMemTable, ISearchTable
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<HashListMemTable>();
        private static readonly IComparer<Tuple<int, long>> MemTableComparer = new MemTableComparer();

        public int Count { get { return _count; } }
        public Guid Id { get { return _id; } }

        private readonly ConcurrentDictionary<uint, SortedList<Tuple<int, long>, byte>> _hash;
        private readonly Guid _id = Guid.NewGuid();
        private int _count;

        private int _isConverting;

        public HashListMemTable(int maxSize)
        {
            _hash = new ConcurrentDictionary<uint, SortedList<Tuple<int, long>, byte>>();
        }

        public bool MarkForConversion()
        {
            return Interlocked.CompareExchange(ref _isConverting, 1, 0) == 0;
        }

        public void Add(uint stream, int version, long position)
        {
            AddEntries(new[] { new IndexEntry(stream, version, position) });
        }

        public void AddEntries(IList<IndexEntry> entries)
        {
            Ensure.NotNull(entries, "entries");
            Ensure.Positive(entries.Count, "entries.Count");

            // only one thread at a time can write
            Interlocked.Add(ref _count, entries.Count);

            var stream = entries[0].Stream; // NOTE: all entries should have the same stream
            SortedList<Tuple<int, long>, byte> list;
            if (!_hash.TryGetValue(stream, out list))
            {
                list = new SortedList<Tuple<int, long>, byte>(MemTableComparer);
                _hash.AddOrUpdate(stream, list, (x, y) => { throw new Exception("This should never happen as MemTable updates are single-threaded."); });
            }

            if (!Monitor.TryEnter(list, 10000))
                throw new UnableToAcquireLockInReasonableTimeException();
            try
            {
                for (int i = 0, n = entries.Count; i < n; ++i)
                {
                    var entry = entries[i];
                    if (entry.Stream != stream)
                        throw new Exception("Not all index entries in a bulk have the same stream hash.");
                    Ensure.Nonnegative(entry.Version, "entry.Version");
                    Ensure.Nonnegative(entry.Position, "entry.Position");
                    list.Add(new Tuple<int, long>(entry.Version, entry.Position), 1);
                }
            }
            finally
            {
                Monitor.Exit(list);
            }
        }

        public bool TryGetOneValue(uint stream, int number, out long position)
        {
            if (number < 0)
                throw new ArgumentOutOfRangeException("number");

            position = 0;

            SortedList<Tuple<int, long>, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000)) throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    int endIdx = list.UpperBound(Tuple.Create(number, long.MaxValue));
                    if (endIdx == -1)
                        return false;

                    var key = list.Keys[endIdx];
                    if (key.Item1 == number)
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

        public IEnumerable<IndexEntry> GetRange(uint stream, int startNumber, int endNumber)
        {
            if (startNumber < 0)
                throw new ArgumentOutOfRangeException("startNumber");
            if (endNumber < 0)
                throw new ArgumentOutOfRangeException("endNumber");

            var ret = new List<IndexEntry>();

            SortedList<Tuple<int, long>, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000)) throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    var endIdx = list.UpperBound(Tuple.Create(endNumber, long.MaxValue));
                    for (int i = endIdx; i >= 0; i--)
                    {
                        var key = list.Keys[i];
                        if (key.Item1 < startNumber)
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