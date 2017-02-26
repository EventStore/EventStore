﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog;
using EventStore.Core.Util;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Index
{
    public class TableIndex : ITableIndex
    {
        public const string IndexMapFilename = "indexmap";
        private const int MaxMemoryTables = 1;

        private static readonly ILogger Log = LogManager.GetLoggerFor<TableIndex>();
        internal static readonly IndexEntry InvalidIndexEntry = new IndexEntry(0, -1, -1);

        public long CommitCheckpoint { get { return Interlocked.Read(ref _commitCheckpoint); } }
        public long PrepareCheckpoint { get { return Interlocked.Read(ref _prepareCheckpoint); } }

        private readonly int _maxSizeForMemory;
        private readonly int _maxTablesPerLevel;
        private readonly bool _additionalReclaim;
        private readonly bool _inMem;
        private readonly int _indexCacheDepth;
        private readonly byte _ptableVersion;
        private readonly string _directory;
        private readonly Func<IMemTable> _memTableFactory;
        private readonly Func<TFReaderLease> _tfReaderFactory;
        private readonly IIndexFilenameProvider _fileNameProvider;

        private readonly object _awaitingTablesLock = new object();

        private IndexMap _indexMap;
        private List<TableItem> _awaitingMemTables;
        private long _commitCheckpoint = -1;
        private long _prepareCheckpoint = -1;

        private volatile bool _backgroundRunning;
        private readonly ManualResetEventSlim _backgroundRunningEvent = new ManualResetEventSlim(true);

        private IHasher _lowHasher;
        private IHasher _highHasher;

        private bool _initialized;

        public TableIndex(string directory,
                          IHasher lowHasher,
                          IHasher highHasher,
                          Func<IMemTable> memTableFactory,
                          Func<TFReaderLease> tfReaderFactory,
                          byte ptableVersion,
                          int maxSizeForMemory = 1000000,
                          int maxTablesPerLevel = 4,
                          bool additionalReclaim = false,
                          bool inMem = false,
                          int indexCacheDepth = 16)
        {
            Ensure.NotNullOrEmpty(directory, "directory");
            Ensure.NotNull(memTableFactory, "memTableFactory");
            Ensure.NotNull(lowHasher, "lowHasher");
            Ensure.NotNull(highHasher, "highHasher");
            Ensure.NotNull(tfReaderFactory, "tfReaderFactory");
            if (maxTablesPerLevel <= 1)
                throw new ArgumentOutOfRangeException("maxTablesPerLevel");

            if(indexCacheDepth > 28 || indexCacheDepth < 8) throw new ArgumentOutOfRangeException("indexCacheDepth");
            _directory = directory;
            _memTableFactory = memTableFactory;
            _tfReaderFactory = tfReaderFactory;
            _fileNameProvider = new GuidFilenameProvider(directory);
            _maxSizeForMemory = maxSizeForMemory;
            _maxTablesPerLevel = maxTablesPerLevel;
            _additionalReclaim = additionalReclaim;
            _inMem = inMem;
            _indexCacheDepth = indexCacheDepth;
            _ptableVersion = ptableVersion;
            _awaitingMemTables = new List<TableItem> { new TableItem(_memTableFactory(), -1, -1) };

            _lowHasher = lowHasher; 
            _highHasher = highHasher;
        }

        public void Initialize(long chaserCheckpoint)
        {
            Ensure.Nonnegative(chaserCheckpoint, "chaserCheckpoint");

            //NOT THREAD SAFE (assumes one thread)
            if (_initialized)
                throw new IOException("TableIndex is already initialized.");
            _initialized = true;

            if (_inMem)
            {
                _indexMap = IndexMap.CreateEmpty(_maxTablesPerLevel);
                _prepareCheckpoint = _indexMap.PrepareCheckpoint;
                _commitCheckpoint = _indexMap.CommitCheckpoint;
                return;
            }

            CreateIfDoesNotExist(_directory);
            var indexmapFile = Path.Combine(_directory, IndexMapFilename);

            // if TableIndex's CommitCheckpoint is >= amount of written TFChunk data, 
            // we'll have to remove some of PTables as they point to non-existent data
            // this can happen (very unlikely, though) on master crash
            try
            {
                _indexMap = IndexMap.FromFile(indexmapFile, maxTablesPerLevel: _maxTablesPerLevel, cacheDepth: _indexCacheDepth);
                if (_indexMap.CommitCheckpoint >= chaserCheckpoint)
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
                _indexMap = IndexMap.FromFile(indexmapFile, maxTablesPerLevel: _maxTablesPerLevel, cacheDepth: _indexCacheDepth);
            }
            _prepareCheckpoint = _indexMap.PrepareCheckpoint;
            _commitCheckpoint = _indexMap.CommitCheckpoint;

            // clean up all other remaining files
            var indexFiles = _indexMap.InOrder().Select(x => Path.GetFileName(x.Filename))
                                                .Union(new[] { IndexMapFilename });
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
                                        string.Format("index-backup-{0:yyyy-MM-dd_HH-mm-ss.fff}", DateTime.UtcNow));
                Log.Error("Making backup of index folder for inspection to {0}...", dumpPath);
                FileUtils.DirectoryCopy(_directory, dumpPath, copySubDirs: true);
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Unexpected error while copying index to backup dir '{0}'", dumpPath);
            }
        }

        private static void CreateIfDoesNotExist(string directory)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        public void Add(long commitPos, string streamId, long version, long position)
        {
            Ensure.Nonnegative(commitPos, "commitPos");
            Ensure.Nonnegative(version, "version");
            Ensure.Nonnegative(position, "position");

            AddEntries(commitPos, new[] { CreateIndexKey(streamId, version, position) });
        }

        public void AddEntries(long commitPos, IList<IndexKey> entries)
        {
            //should only be called on a single thread.
            var table = (IMemTable)_awaitingMemTables[0].Table; // always a memtable

            var collection = entries.Select(x => CreateIndexEntry(x)).ToList();
            table.AddEntries(collection);

            if (table.Count >= _maxSizeForMemory)
            {
                long prepareCheckpoint = collection[0].Position;
                for (int i = 1, n = collection.Count; i < n; ++i)
                {
                    prepareCheckpoint = Math.Max(prepareCheckpoint, collection[i].Position);
                }

                lock (_awaitingTablesLock)
                {
                    var newTables = new List<TableItem> { new TableItem(_memTableFactory(), -1, -1) };
                    newTables.AddRange(_awaitingMemTables.Select(
                        (x, i) => i == 0 ? new TableItem(x.Table, prepareCheckpoint, commitPos) : x));

                    Log.Trace("Switching MemTable, currently: {0} awaiting tables.", newTables.Count);

                    _awaitingMemTables = newTables;
                    if (_inMem) return;
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
                        ptable = PTable.FromMemtable(memtable, _fileNameProvider.GetFilenameNewTable(), _indexCacheDepth);
                    }
                    else
                        ptable = (PTable)tableItem.Table;

                    var indexmapFile = Path.Combine(_directory, IndexMapFilename);

                    MergeResult mergeResult;
                    using (var reader = _tfReaderFactory())
                    {
                        mergeResult = _indexMap.AddPTable(ptable, tableItem.PrepareCheckpoint, tableItem.CommitCheckpoint,
                                                          (streamId, currentHash) => UpgradeHash(streamId, currentHash),
                                                          entry => reader.ExistsAt(entry.Position),
                                                          entry => ReadEntry(reader, entry.Position), _fileNameProvider, _ptableVersion, _indexCacheDepth);
                    }
                    _indexMap = mergeResult.MergedMap;
                    _indexMap.SaveToFile(indexmapFile);

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
                    mergeResult.ToDelete.ForEach(x => x.MarkForDestruction());
                }
            }
            catch (FileBeingDeletedException exc)
            {
                Log.ErrorException(exc, "Could not acquire chunk in TableIndex.ReadOffQueue. It is OK if node is shutting down.");
            }
            catch (Exception exc)
            {
                Log.ErrorException(exc, "Error in TableIndex.ReadOffQueue");
                throw;
            }
        }

        private static Tuple<string, bool> ReadEntry(TFReaderLease reader, long position)
        {
            RecordReadResult result = reader.TryReadAt(position);
            if (!result.Success)
                return new Tuple<string, bool>(String.Empty, false);
            if (result.LogRecord.RecordType != TransactionLog.LogRecords.LogRecordType.Prepare)
                throw new Exception(string.Format("Incorrect type of log record {0}, expected Prepare record.",
                                                  result.LogRecord.RecordType));
            return new Tuple<string, bool>(((TransactionLog.LogRecords.PrepareLogRecord)result.LogRecord).EventStreamId, true);
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

                var ptable = PTable.FromMemtable(memtable, _fileNameProvider.GetFilenameNewTable(), _indexCacheDepth);
                var swapped = false;
                lock (_awaitingTablesLock)
                {
                    for (var j = _awaitingMemTables.Count - 1; j >= 1; j--)
                    {
                        var tableItem = _awaitingMemTables[j];
                        if (!(tableItem.Table is IMemTable) || tableItem.Table.Id != ptable.Id) continue;
                        swapped = true;
                        _awaitingMemTables[j] = new TableItem(ptable,
                            tableItem.PrepareCheckpoint,
                            tableItem.CommitCheckpoint);
                        break;
                    }
                }
                if (!swapped)
                    ptable.MarkForDestruction();
                toPutOnDisk--;
            }
        }

        public bool TryGetOneValue(string streamId, long version, out long position)
        {
            ulong stream = CreateHash(streamId);
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
            throw new InvalidOperationException("Files are locked.");
        }

        private bool TryGetOneValueInternal(ulong stream, long version, out long position)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException("version");

            var awaiting = _awaitingMemTables;
            foreach (var tableItem in awaiting)
            {
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

        public bool TryGetLatestEntry(string streamId, out IndexEntry entry)
        {
            ulong stream = CreateHash(streamId);
            var counter = 0;
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
            throw new InvalidOperationException("Files are locked.");
        }

        private bool TryGetLatestEntryInternal(ulong stream, out IndexEntry entry)
        {
            var awaiting = _awaitingMemTables;
            foreach (var t in awaiting)
            {
                if (t.Table.TryGetLatestEntry(stream, out entry))
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

        public bool TryGetOldestEntry(string streamId, out IndexEntry entry)
        {
            ulong stream = CreateHash(streamId);
            var counter = 0;
            while (counter < 5)
            {
                counter++;
                try
                {
                    return TryGetOldestEntryInternal(stream, out entry);
                }
                catch (FileBeingDeletedException)
                {
                    Log.Trace("File being deleted.");
                }
            }
            throw new InvalidOperationException("Files are locked.");
        }

        private bool TryGetOldestEntryInternal(ulong stream, out IndexEntry entry)
        {
            var map = _indexMap;
            foreach (var table in map.InReverseOrder())
            {
                if (table.TryGetOldestEntry(stream, out entry))
                    return true;
            }

            var awaiting = _awaitingMemTables;
            for (var index = awaiting.Count - 1; index >= 0; index--)
            {
                if (awaiting[index].Table.TryGetOldestEntry(stream, out entry))
                    return true;
            }

            entry = InvalidIndexEntry;
            return false;
        }

        public IEnumerable<IndexEntry> GetRange(string streamId, long startVersion, long endVersion, int? limit = null)
        {
            ulong hash = CreateHash(streamId);
            var counter = 0;
            while (counter < 5)
            {
                counter++;
                try
                {
                    return GetRangeInternal(hash, startVersion, endVersion, limit);
                }
                catch (FileBeingDeletedException)
                {
                    Log.Trace("File being deleted.");
                }
            }
            throw new InvalidOperationException("Files are locked.");
        }

        private IEnumerable<IndexEntry> GetRangeInternal(ulong hash, long startVersion, long endVersion, int? limit = null)
        {
            if (startVersion < 0)
                throw new ArgumentOutOfRangeException("startVersion");
            if (endVersion < 0)
                throw new ArgumentOutOfRangeException("endVersion");

            var candidates = new List<IEnumerator<IndexEntry>>();

            var awaiting = _awaitingMemTables;
            for (int index = 0; index < awaiting.Count; index++)
            {
                var range = awaiting[index].Table.GetRange(hash, startVersion, endVersion, limit).GetEnumerator();
                if (range.MoveNext())
                    candidates.Add(range);
            }

            var map = _indexMap;
            foreach (var table in map.InOrder())
            {
                var range = table.GetRange(hash, startVersion, endVersion, limit).GetEnumerator();
                if (range.MoveNext())
                    candidates.Add(range);
            }

            var last = new IndexEntry(0, 0, 0);
            var first = true;
            while (candidates.Count > 0)
            {
                var maxIdx = GetMaxOf(candidates);
                var winner = candidates[maxIdx];

                var best = winner.Current;
                if (first || ((last.Stream != best.Stream) && (last.Version != best.Version)) || last.Position != best.Position)
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
            var max = new IndexEntry(ulong.MinValue, 0, long.MinValue);
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
            if (!_backgroundRunningEvent.Wait(7000))
                throw new TimeoutException("Could not finish background thread in reasonable time.");
            if (_inMem)
                return;
            if (_indexMap == null) return;
            if (removeFiles)
            {
                _indexMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
                File.Delete(Path.Combine(_directory, IndexMapFilename));
            }
            else
            {
                _indexMap.InOrder().ToList().ForEach(x => x.Dispose());
            }
            _indexMap.InOrder().ToList().ForEach(x => x.WaitForDisposal(TimeSpan.FromMilliseconds(5000)));
        }

        private IndexEntry CreateIndexEntry(IndexKey key)
        {
            key = CreateIndexKey(key.StreamId, key.Version, key.Position);
            return new IndexEntry(key.Hash, key.Version, key.Position);
        }

        private ulong UpgradeHash(string streamId, ulong lowHash)
        {
            return lowHash << 32 | _highHasher.Hash(streamId);
        }

        private ulong CreateHash(string streamId)
        {
            return (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);
        }

        private IndexKey CreateIndexKey(string streamId, long version, long position)
        {
            return new IndexKey(streamId, version, position, CreateHash(streamId));
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
