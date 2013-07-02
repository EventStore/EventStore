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
using System.Text;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.Util;

namespace EventStore.Core.Index
{
    public class TableIndex : ITableIndex
    {
        public const string IndexMapFilename = "indexmap";
        public const string IndexMapBackupFilename = "indexmap.backup";
        private const int MaxMemoryTables = 1;

        private static readonly ILogger Log = LogManager.GetLoggerFor<TableIndex>();
        internal static readonly IndexEntry InvalidIndexEntry = new IndexEntry(0, -1, -1);

        public long CommitCheckpoint { get { return Interlocked.Read(ref _commitCheckpoint); } }
        public long PrepareCheckpoint { get { return Interlocked.Read(ref _prepareCheckpoint); } }

        private readonly int _maxSizeForMemory;
        private readonly int _maxTablesPerLevel;
        private readonly bool _additionalReclaim;
        private readonly string _directory;
        private readonly Func<IMemTable> _memTableFactory;
        private readonly IIndexFilenameProvider _fileNameProvider;

        private readonly object _awaitingTablesLock = new object();

        private IndexMap _indexMap;
        private List<TableItem> _awaitingMemTables;
        private long _commitCheckpoint = -1;
        private long _prepareCheckpoint = -1;

        private volatile bool _backgroundRunning;
        private readonly ManualResetEventSlim _backgroundRunningEvent = new ManualResetEventSlim(true);

        private bool _initialized;

        public TableIndex(string directory, 
                          Func<IMemTable> memTableFactory, 
                          int maxSizeForMemory = 1000000,
                          int maxTablesPerLevel = 4,
                          bool additionalReclaim = false)
        {
            Ensure.NotNullOrEmpty(directory, "directory");
            Ensure.NotNull(memTableFactory, "memTableFactory");
            if (maxTablesPerLevel <= 1)
                throw new ArgumentOutOfRangeException("maxTablesPerLevel");

            _directory = directory;
            _memTableFactory = memTableFactory;
            _fileNameProvider = new GuidFilenameProvider(directory);
            _maxSizeForMemory = maxSizeForMemory;
            _maxTablesPerLevel = maxTablesPerLevel;
            _additionalReclaim = additionalReclaim;
            _awaitingMemTables = new List<TableItem> { new TableItem(_memTableFactory(), -1, -1) };
        }

        public void Initialize(long writerCheckpoint)
        {
            Ensure.Nonnegative(writerCheckpoint, "writerCheckpoint");

            //NOT THREAD SAFE (assumes one thread)
            if (_initialized)
                throw new IOException("TableIndex is already initialized.");           
            _initialized = true;
            
            CreateIfDoesNotExist(_directory);
            var indexmapFile = Path.Combine(_directory, IndexMapFilename);
            var backupFile = Path.Combine(_directory, IndexMapBackupFilename);

            // if TableIndex's CommitCheckpoint is >= amount of written TFChunk data, 
            // we'll have to remove some of PTables as they point to non-existent data
            // this can happen (very unlikely, though) on master crash
            try
            {
                if (IsCorrupt(_directory))
                    throw new CorruptIndexException("IndexMap is in unsafe state.");
                _indexMap = IndexMap.FromFile(indexmapFile, IsHashCollision, _maxTablesPerLevel);
                if (_indexMap.CommitCheckpoint >= writerCheckpoint)
                {
                    _indexMap.Dispose(TimeSpan.FromMilliseconds(5000));
                    throw new CorruptIndexException("IndexMap's CommitCheckpoint is greater than WriterCheckpoint.");
                }
            }
            catch (CorruptIndexException exc)
            {
                Log.ErrorException(exc, "ReadIndex is corrupted...");
                LogIndexMapContent(indexmapFile);
                DumpAndCopyIndex();
                File.Delete(indexmapFile);

                bool createEmptyIndexMap = true;
                if (File.Exists(backupFile))
                {
                    File.Copy(backupFile, indexmapFile);
                    try
                    {
                        _indexMap = IndexMap.FromFile(indexmapFile, IsHashCollision, _maxTablesPerLevel);
                        if (_indexMap.CommitCheckpoint >= writerCheckpoint)
                        {
                            _indexMap.Dispose(TimeSpan.FromMilliseconds(5000));
                            throw new CorruptIndexException("Back-up IndexMap's CommitCheckpoint is still greater than WriterCheckpoint.");
                        }
                        createEmptyIndexMap = false;
                        Log.Info("Using back-up index map...");
                    }
                    catch (CorruptIndexException ex)
                    {
                        Log.ErrorException(ex, "Backup IndexMap is also corrupted...");
                        LogIndexMapContent(backupFile);
                        File.Delete(indexmapFile);
                        File.Delete(backupFile);
                    }
                }

                if (createEmptyIndexMap)
                    _indexMap = IndexMap.FromFile(indexmapFile, IsHashCollision, _maxTablesPerLevel);
                if (IsCorrupt(_directory))
                    LeaveUnsafeState(_directory);
            }
            _prepareCheckpoint = _indexMap.PrepareCheckpoint;
            _commitCheckpoint = _indexMap.CommitCheckpoint;

            // clean up all other remaining files
            var indexFiles = _indexMap.InOrder().Select(x => Path.GetFileName(x.Filename))
                                                .Union(new[] { IndexMapFilename, IndexMapBackupFilename });
            var toDeleteFiles = Directory.EnumerateFiles(_directory).Select(Path.GetFileName)
                                         .Except(indexFiles, StringComparer.OrdinalIgnoreCase);
            foreach (var filePath in toDeleteFiles)
            {
                var file = Path.Combine(_directory, filePath);
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
        }

        private static void LogIndexMapContent(string indexmapFile)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendFormat("IndexMap '{0}' content:\n", indexmapFile);
                sb.AppendLine(Helper.FormatBinaryDump(File.ReadAllBytes(indexmapFile)));

                Log.Error(sb.ToString());
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Unexpected error while dumping IndexMap '{0}'.", indexmapFile);
            }
        }

        private void DumpAndCopyIndex()
        {
            string dumpPath = null;
            try
            {
                dumpPath = Path.Combine(Path.GetDirectoryName(_directory),
                                        string.Format("index-backup-{0:yyyy-MM-dd_hh-mm-ss.fff}", DateTime.UtcNow));
                Log.Error("Making backup of index folder for inspection to {0}...", dumpPath);
                FileUtils.DirectoryCopy(_directory, dumpPath, copySubDirs: true);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Unexpected error while copying index to backup dir '{0}'", dumpPath);
            }
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

        public void Add(long commitPos, uint stream, int version, long position)
        {
            Ensure.Nonnegative(commitPos, "commitPos");
            Ensure.Nonnegative(version, "version");
            Ensure.Nonnegative(position, "position");

            AddEntries(commitPos, new[] { new IndexEntry(stream, version, position) });
        }

        public void AddEntries(long commitPos, IList<IndexEntry> entries)
        {
            Ensure.Nonnegative(commitPos, "commitPos");
            Ensure.NotNull(entries, "entries");
            Ensure.Positive(entries.Count, "entries.Count");

            //should only be called on a single thread.
            var table = (IMemTable)_awaitingMemTables[0].Table; // always a memtable

            table.AddEntries(entries);

            if (table.Count >= _maxSizeForMemory)
            {
                long prepareCheckpoint = entries[0].Position;
                for (int i = 1, n = entries.Count; i < n; ++i)
                {
                    prepareCheckpoint = Math.Max(prepareCheckpoint, entries[i].Position);
                }

                lock (_awaitingTablesLock)
                {
                    var newTables = new List<TableItem> { new TableItem(_memTableFactory(), -1, -1) };
                    newTables.AddRange(_awaitingMemTables.Select(
                        (x, i) => i == 0 ? new TableItem(x.Table, prepareCheckpoint, commitPos) : x));

                    Log.Trace("Switching MemTable, currently: {0} awaiting tables.", newTables.Count);

                    _awaitingMemTables = newTables;
                    if (!_backgroundRunning)
                    {
                        _backgroundRunningEvent.Reset();
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
                            _backgroundRunningEvent.Set(); 
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

                    // backup current version of IndexMap in case following switch will be left in unsafe state
                    // this will allow to rebuild just part of index
                    var backupFile = Path.Combine(_directory, IndexMapBackupFilename);
                    var indexmapFile = Path.Combine(_directory, IndexMapFilename);
                    Helper.EatException(() =>
                    {
                        if (File.Exists(backupFile))
                            File.Delete(backupFile);
                        if (File.Exists(indexmapFile))
                            File.Copy(indexmapFile, backupFile);
                    });

                    EnterUnsafeState(_directory);

                    var mergeResult = _indexMap.AddFile(ptable, tableItem.PrepareCheckpoint, tableItem.CommitCheckpoint, _fileNameProvider);
                    _indexMap = mergeResult.MergedMap;
                    _indexMap.SaveToFile(indexmapFile);

                    LeaveUnsafeState(_directory);

                    lock (_awaitingTablesLock)
                    {
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

                    // We'll keep indexmap.backup in case of crash. In case of crash we hope that all necessary 
                    // PTables for previous version of IndexMap are still there, so we can rebuild
                    // from last step, not to do full rebuild.
                    mergeResult.ToDelete.ForEach(x => x.MarkForDestruction());
                }
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error in TableIndex.ReadOffQueue");
                throw;
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

        public void Close(bool removeFiles = true)
        {
            //this should also make sure that no background tasks are running anymore
            if (!_backgroundRunningEvent.Wait(7000))
                throw new TimeoutException("Could not finish background thread in reasonable time.");

            if (_indexMap != null)
            {
                if (removeFiles)
                {
                    _indexMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
                    _indexMap.InOrder().ToList().ForEach(x => x.WaitForDisposal(TimeSpan.FromMilliseconds(5000)));
                    File.Delete(Path.Combine(_directory, IndexMapFilename));
                }
                else
                {
                    _indexMap.InOrder().ToList().ForEach(x => x.Dispose());
                    _indexMap.InOrder().ToList().ForEach(x => x.WaitForDisposal(TimeSpan.FromMilliseconds(5000)));
                }
            }
        }

        public static bool IsCorrupt(string directory)
        {
            return File.Exists(Path.Combine(directory, "merging.m"));
        }

        public static void EnterUnsafeState(string directory)
        {
            if (!IsCorrupt(directory))
            {
                using (var f = File.Create(Path.Combine(directory, "merging.m")))
                {
                    f.Flush(flushToDisk: true);
                }
            }
        }

        public static void LeaveUnsafeState(string directory)
        {
            File.Delete(Path.Combine(directory, "merging.m"));
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
