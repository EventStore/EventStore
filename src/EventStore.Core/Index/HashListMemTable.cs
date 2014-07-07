using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;

namespace EventStore.Core.Index
{
    public class HashListMemTable : IMemTable, ISearchTable
    {
        private static readonly IComparer<Entry> MemTableComparer = new EntryComparer();

        public int Count { get { return _count; } }
        public Guid Id { get { return _id; } }

        private readonly ConcurrentDictionary<uint, SortedList<Entry, byte>> _hash;
        private readonly Guid _id = Guid.NewGuid();
        private int _count;

        private int _isConverting;

        public HashListMemTable(int maxSize)
        {
            _hash = new ConcurrentDictionary<uint, SortedList<Entry, byte>>();
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
            SortedList<Entry, byte> list;
            if (!_hash.TryGetValue(stream, out list))
            {
                list = new SortedList<Entry, byte>(MemTableComparer);
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
                    list.Add(new Entry(entry.Version, entry.Position), 0);
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

            SortedList<Entry, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000)) throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    int endIdx = list.UpperBound(new Entry(number, long.MaxValue));
                    if (endIdx == -1)
                        return false;

                    var key = list.Keys[endIdx];
                    if (key.EvNum == number)
                    {
                        position = key.LogPos;
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

            SortedList<Entry, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000))
                    throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    var latest = list.Keys[list.Count - 1];
                    entry = new IndexEntry(stream, latest.EvNum, latest.LogPos);
                    return true;
                }
                finally
                {
                    Monitor.Exit(list);
                }
            }
            return false;
        }

        public bool TryGetOldestEntry(uint stream, out IndexEntry entry)
        {
            entry = TableIndex.InvalidIndexEntry;

            SortedList<Entry, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000))
                    throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    var oldest = list.Keys[0];
                    entry = new IndexEntry(stream, oldest.EvNum, oldest.LogPos);
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
            //Log.Trace("Sorting array in HashListMemTable.IterateAllInOrder...");

            var keys = _hash.Keys.ToArray();
            Array.Sort(keys, new ReverseComparer<uint>());
            
            foreach (var key in keys)
            {
                var list = _hash[key];
                for (int i = list.Count - 1; i >= 0; --i)
                {
                    var x = list.Keys[i];
                    yield return new IndexEntry(key, x.EvNum, x.LogPos);
                }
            }
            //Log.Trace("Sorting array in HashListMemTable.IterateAllInOrder... DONE!");
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

            SortedList<Entry, byte> list;
            if (_hash.TryGetValue(stream, out list))
            {
                if (!Monitor.TryEnter(list, 10000)) throw new UnableToAcquireLockInReasonableTimeException();
                try
                {
                    var endIdx = list.UpperBound(new Entry(endNumber, long.MaxValue));
                    for (int i = endIdx; i >= 0; i--)
                    {
                        var key = list.Keys[i];
                        if (key.EvNum < startNumber)
                            break;
                        ret.Add(new IndexEntry(stream, version: key.EvNum, position: key.LogPos));
                    }
                }
                finally
                {
                    Monitor.Exit(list);
                }
            }
            return ret;
        }

        private struct Entry
        {
            public readonly int EvNum;
            public readonly long LogPos;

            public Entry(int evNum, long logPos)
            {
                EvNum = evNum;
                LogPos = logPos;
            }
        }

        private class EntryComparer : IComparer<Entry>
        {
            public int Compare(Entry x, Entry y)
            {
                if (x.EvNum < y.EvNum) return -1;
                if (x.EvNum > y.EvNum) return 1;
                if (x.LogPos < y.LogPos) return -1;
                if (x.LogPos > y.LogPos) return 1;
                return 0;
            }
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