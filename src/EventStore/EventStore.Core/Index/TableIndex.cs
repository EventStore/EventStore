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
using System.IO;
using System.Linq;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;

namespace EventStore.Core.Index
{
    public class TableIndex : ITableIndex
    {
        public const string IndexMapFilename = "indexmap";
        private const int MaxMemoryTables = 1;

        private static readonly ILogger Log = LogManager.GetLoggerFor<TableIndex>();
        internal static readonly IndexEntry InvalidIndexEntry = new IndexEntry(0, -1, -1);

        public ICheckpoint CommitCheckpoint { get { return _commitCheckpoint; } }
        public long PrepareCheckpoint { get { return Interlocked.Read(ref _prepareCheckpoint); } }

        private readonly int _maxSizeForMemory;
        private readonly int _maxTablesPerLevel;
        private readonly bool _additionalReclaim;
        private readonly string _directory;
        private readonly Func<IMemTable> _memTableFactory;
        private readonly IIndexFilenameProvider _fileNameProvider;
        private readonly ICheckpoint _commitCheckpoint;

        private readonly object _awaitingTablesLock = new object();

        private IndexMap _indexMap;
        private List<TableItem> _awaitingMemTables;
        private long _prepareCheckpoint = -1;

        private volatile bool _backgroundRunning;
        private readonly ManualResetEvent _backgroundRunningEvent = new ManualResetEvent(true);

        public TableIndex(string directory, 
                          Func<IMemTable> memTableFactory, 
                          ICheckpoint commitCheckpoint,
                          int maxSizeForMemory = 1000000, 
                          int maxTablesPerLevel = 4,
                          bool additionalReclaim = false)
        {
            Ensure.NotNullOrEmpty(directory, "directory");
            Ensure.NotNull(memTableFactory, "memTableFactory");
            Ensure.NotNull(commitCheckpoint, "CommitCheckpoint");
            if (maxTablesPerLevel <= 1)
                throw new ArgumentOutOfRangeException("maxTablesPerLevel");

            _directory = directory;
            _memTableFactory = memTableFactory;
            _fileNameProvider = new GuidFilenameProvider(directory);
            _commitCheckpoint = commitCheckpoint;
            _maxSizeForMemory = maxSizeForMemory;
            _maxTablesPerLevel = maxTablesPerLevel;
            _additionalReclaim = additionalReclaim;
            _awaitingMemTables = new List<TableItem> { new TableItem(_memTableFactory(), -1, -1) };
        }

        public void Initialize()
        {
            //NOT THREAD SAFE (assumes one thread)
            CreateIfDoesNotExist(_directory);

            try
            {
                _indexMap = IndexMap.FromFile(Path.Combine(_directory, IndexMapFilename),
                                              IsHashCollision,
                                              _maxTablesPerLevel);
                if (_indexMap.IsCorrupt(_directory))
                    throw new CorruptIndexException("IndexMap is in unsafe state.");
            }
            catch (CorruptIndexException exc)
            {
                Log.ErrorException(exc, "ReadIndex was corrupted. Rebuilding from scratch...");
                foreach (var filePath in Directory.EnumerateFiles(_directory))
                {
                    File.Delete(filePath);
                }
                _indexMap = IndexMap.FromFile(Path.Combine(_directory, IndexMapFilename),
                                              IsHashCollision,
                                              _maxTablesPerLevel);
            } 

            _prepareCheckpoint = _indexMap.PrepareCheckpoint;
            _commitCheckpoint.Write(_indexMap.CommitCheckpoint);
        }

        private bool IsHashCollision(IndexEntry entry)
        {
            // TODO AN fails on case where one stream is already deleted, and another is present, but both have same hash
            using (var enumerator = GetRange(entry.Stream, 0, 0).GetEnumerator())
            {
                return enumerator.MoveNext() && enumerator.MoveNext(); // at least two entries -- hash collision
            }
        }

        private static void CreateIfDoesNotExist(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }        

        public void Add(uint stream, int version, long position)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException("version");
            if (position < 0)
                throw new ArgumentOutOfRangeException("position");

            //should only be called on a single thread.
            var table = (IMemTable) _awaitingMemTables[0].Table; // always a memtable
            table.Add(stream, version, position);

            if (table.Count >= _maxSizeForMemory)
            {
                var prepareCheckpoint = position;
                var commitCheckpoint = _commitCheckpoint.ReadNonFlushed();

                lock (_awaitingTablesLock)
                {
                    var newTables = new List<TableItem> {new TableItem(_memTableFactory(), -1, -1)};
                    newTables.AddRange(_awaitingMemTables.Select(
                        (x, i) => i == 0 ? new TableItem(x.Table, prepareCheckpoint, commitCheckpoint) : x));

                    Log.Trace("Switching MemTable, currently: {0} awaiting tables.", newTables.Count);

                    _awaitingMemTables = newTables;
                    if (!_backgroundRunning)
                    {
                        _backgroundRunning = true;
                        ThreadPool.QueueUserWorkItem(x => ReadOffQueue());
                    }

                    if (_additionalReclaim)
                        ThreadPool.QueueUserWorkItem(x => ReclaimMemoryIfNeeded(_awaitingMemTables));
                }
            }
        }

        private void ReadOffQueue()
        {
            _backgroundRunningEvent.Reset();
            try
            {
                while (true)
                {
                    TableItem tableItem;
                    //ISearchTable table;
                    lock (_awaitingTablesLock)
                    {
                        Log.Trace("Awaiting tables queue size is: {0}.", _awaitingMemTables.Count);
                        if (_awaitingMemTables.Count == 1)
                        {
                            _backgroundRunning = false;
                            return;
                        }
                        tableItem = _awaitingMemTables[_awaitingMemTables.Count - 1];
                    }
                    
                    PTable ptable;
                    var memtable = tableItem.Table as IMemTable;
                    if (memtable != null)
                    {
                        memtable.MarkForConversion();
                        ptable = PTable.FromMemtable(memtable, _fileNameProvider.GetFilenameNewTable());
                    }
                    else
                        ptable = (PTable) tableItem.Table;

                    _indexMap.EnterUnsafeState(_directory);

                    var mergeResult = _indexMap.AddFile(ptable,
                                                        tableItem.PrepareCheckpoint,
                                                        tableItem.CommitCheckpoint,
                                                        _fileNameProvider);
                    _indexMap = mergeResult.MergedMap;
                    _indexMap.SaveToFile(Path.Combine(_directory, IndexMapFilename));

                    _indexMap.LeaveUnsafeState(_directory);
                    
                    lock (_awaitingTablesLock)
                    {
                        //oh well at least its only a small lock that is very unlikely to ever be hit
                        var memTables = _awaitingMemTables.ToList();

                        var corrTable = memTables.First(x => x.Table.Id == ptable.Id);
                        memTables.Remove(corrTable);

                        // parallel thread could already switch table, 
                        // so if we have another PTable instance with same ID,
                        // we need to kill that instance as we added ours already
                        if (!ReferenceEquals(corrTable.Table, ptable) && corrTable.Table is PTable)
                            ((PTable)corrTable.Table).MarkForDestruction(); 

                        Log.Trace("There are now {0} awaiting tables.", memTables.Count);
                        _awaitingMemTables = memTables;
                    }

                    mergeResult.ToDelete.ForEach(x => x.MarkForDestruction());
                }
            }
            finally
            {
                _backgroundRunning = false;
                _backgroundRunningEvent.Set();
            }
        }

        private void ReclaimMemoryIfNeeded(List<TableItem> awaitingMemTables)
        {
            var toPutOnDisk = awaitingMemTables.OfType<IMemTable>().Count() - MaxMemoryTables;
            for (var i = awaitingMemTables.Count - 1; i >= 1 && toPutOnDisk > 0; i--)
            {
                var memtable = awaitingMemTables[i].Table as IMemTable;
                if (memtable == null || !memtable.MarkForConversion()) 
                    continue;

                Log.Trace("Putting awaiting file as PTable instead of MemTable [{0}].", memtable.Id);
                    
                var ptable = PTable.FromMemtable(memtable, _fileNameProvider.GetFilenameNewTable());
                bool swapped = false;
                lock (_awaitingTablesLock)
                {
                    for (int j = _awaitingMemTables.Count - 1; j >= 1; j--)
                    {
                        var tableItem = _awaitingMemTables[j];
                        if (tableItem.Table is IMemTable && tableItem.Table.Id == ptable.Id)
                        {
                            swapped = true;
                            _awaitingMemTables[j] = new TableItem(ptable,
                                                                  tableItem.PrepareCheckpoint,
                                                                  tableItem.CommitCheckpoint);
                            break;
                        }
                    }
                }
                if (!swapped)
                    ptable.MarkForDestruction();
                toPutOnDisk--;
            }
        }

        public bool TryGetOneValue(uint stream, int version, out long position)
        {
            int counter = 0;
            while (counter < 5)
            {
                counter++;
                try
                {
                    return TryGetOneValueInternal(stream, version, out position);
                }
                catch (FileBeingDeletedException)
                {
                    Log.Trace("File being deleted.");
                }
            }
            throw new InvalidOperationException("Something is wrong. Files are locked.");
        }

        private bool TryGetOneValueInternal(uint stream, int version, out long position)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException("version");

            var awaiting = _awaitingMemTables;
            for (int index = 0; index < awaiting.Count; index++)
            {
                var tableItem = awaiting[index];
                if (tableItem.Table.TryGetOneValue(stream, version, out position))
                    return true;
            }

            var map = _indexMap;
            foreach (var table in map.InOrder())
            {
                if (table.TryGetOneValue(stream, version, out position))
                    return true;
            }

            position = 0;
            return false;
        }

        public bool TryGetLatestEntry(uint stream, out IndexEntry entry)
        {
            int counter = 0;
            while (counter < 5)
            {
                counter++;
                try
                {
                    return TryGetLatestEntryInternal(stream, out entry);
                }
                catch (FileBeingDeletedException)
                {
                    Log.Trace("File being deleted.");
                }
            }
            throw new InvalidOperationException("Something is wrong. Files are locked.");
        }

        private bool TryGetLatestEntryInternal(uint stream, out IndexEntry entry)
        {
            var awaiting = _awaitingMemTables;
            for (int index = 0; index < awaiting.Count; index++)
            {
                if (awaiting[index].Table.TryGetLatestEntry(stream, out entry))
                    return true;
            }

            var map = _indexMap;
            foreach (var table in map.InOrder())
            {
                if (table.TryGetLatestEntry(stream, out entry))
                    return true;
            }

            entry = InvalidIndexEntry;
            return false;
        }

        public IEnumerable<IndexEntry> GetRange(uint stream, int startVersion, int endVersion)
        {
            int counter = 0;
            while (counter < 5)
            {
                counter++;
                try
                {
                    return GetRangeInternal(stream, startVersion, endVersion);
                }
                catch (FileBeingDeletedException)
                {
                    Log.Trace("File being deleted.");
                }
            }
            throw new InvalidOperationException("Something is wrong. Files are locked.");
        }

        private IEnumerable<IndexEntry> GetRangeInternal(uint stream, int startVersion, int endVersion)
        {
            if (startVersion < 0)
                throw new ArgumentOutOfRangeException("startVersion");
            if (endVersion < 0)
                throw new ArgumentOutOfRangeException("endVersion");

            var candidates = new List<IEnumerator<IndexEntry>>();

            var awaiting = _awaitingMemTables;
            for (int index = 0; index < awaiting.Count; index++)
            {
                var range = awaiting[index].Table.GetRange(stream, startVersion, endVersion).GetEnumerator();
                if (range.MoveNext())
                    candidates.Add(range);
            }

            var map = _indexMap;
            foreach (var table in map.InOrder())
            {
                var range = table.GetRange(stream, startVersion, endVersion).GetEnumerator();
                if (range.MoveNext())
                    candidates.Add(range);
            }

            var last = new IndexEntry(0, 0, 0);
            bool first = true;
            while (candidates.Count > 0)
            {
                var maxIdx = GetMaxOf(candidates);
                var winner = candidates[maxIdx];
                
                var best = winner.Current;
                if (first || last.Key != best.Key || last.Position != best.Position)
                {
                    last = best;
                    yield return best;
                    first = false;
                }

                if (!winner.MoveNext())
                    candidates.RemoveAt(maxIdx);
            }
        }

        private static int GetMaxOf(List<IEnumerator<IndexEntry>> enumerators)
        {
            var max = new IndexEntry(ulong.MinValue, long.MinValue);
            int idx = 0;
            for (int i = 0; i < enumerators.Count; i++)
            {
                var cur = enumerators[i].Current;
                if (cur.CompareTo(max) > 0)
                {
                    max = cur;
                    idx = i;
                }
            }
            return idx;
        }


        public void ClearAll(bool removeFiles = true)
        {
            _awaitingMemTables = new List<TableItem> { new TableItem(_memTableFactory(), -1, -1) };

            _backgroundRunningEvent.WaitOne(1000);
            //this should also make sure that no background tasks are running anymore

            if (_indexMap != null)
            {
                if (removeFiles)
                {
                    _indexMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
                    _indexMap.InOrder().ToList().ForEach(x => x.WaitForDestroy(1000));
                }
                else
                {
                    _indexMap.InOrder().ToList().ForEach(x => x.Dispose());
                }
            }
        }

        private class TableItem
        {
            public readonly ISearchTable Table;
            public readonly long PrepareCheckpoint;
            public readonly long CommitCheckpoint;

            public TableItem(ISearchTable table, long prepareCheckpoint, long commitCheckpoint)
            {
                Table = table;
                PrepareCheckpoint = prepareCheckpoint;
                CommitCheckpoint = commitCheckpoint;
            }
        }
    }
}
