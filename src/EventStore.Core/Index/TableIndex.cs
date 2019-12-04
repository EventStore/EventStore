using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog;
using EventStore.Core.Util;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;

namespace EventStore.Core.Index {
	public class TableIndex : ITableIndex {
		public const string IndexMapFilename = "indexmap";
		private const int MaxMemoryTables = 1;

		private static readonly ILogger Log = LogManager.GetLoggerFor<TableIndex>();
		internal static readonly IndexEntry InvalidIndexEntry = new IndexEntry(0, -1, -1);

		public long CommitCheckpoint {
			get { return Interlocked.Read(ref _commitCheckpoint); }
		}

		public long PrepareCheckpoint {
			get { return Interlocked.Read(ref _prepareCheckpoint); }
		}

		private readonly int _maxSizeForMemory;
		private readonly int _maxTablesPerLevel;
		private readonly bool _additionalReclaim;
		private readonly bool _inMem;
		private readonly bool _skipIndexVerify;
		private readonly int _indexCacheDepth;
		private readonly int _initializationThreads;
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
		public const string ForceIndexVerifyFilename = ".forceverify";
		private readonly int _maxAutoMergeIndexLevel;
		private readonly int _pTableMaxReaderCount;

		public TableIndex(string directory,
			IHasher lowHasher,
			IHasher highHasher,
			Func<IMemTable> memTableFactory,
			Func<TFReaderLease> tfReaderFactory,
			byte ptableVersion,
			int maxAutoMergeIndexLevel,
			int pTableMaxReaderCount,
			int maxSizeForMemory = 1000000,
			int maxTablesPerLevel = 4,
			bool additionalReclaim = false,
			bool inMem = false,
			bool skipIndexVerify = false,
			int indexCacheDepth = 16,
			int initializationThreads = 1) {
			Ensure.NotNullOrEmpty(directory, "directory");
			Ensure.NotNull(memTableFactory, "memTableFactory");
			Ensure.NotNull(lowHasher, "lowHasher");
			Ensure.NotNull(highHasher, "highHasher");
			Ensure.NotNull(tfReaderFactory, "tfReaderFactory");
			Ensure.Positive(initializationThreads, "initializationThreads");
			Ensure.Positive(pTableMaxReaderCount, "pTableMaxReaderCount");

			if (maxTablesPerLevel <= 1)
				throw new ArgumentOutOfRangeException("maxTablesPerLevel");

			if (indexCacheDepth > 28 || indexCacheDepth < 8) throw new ArgumentOutOfRangeException("indexCacheDepth");

			_directory = directory;
			_memTableFactory = memTableFactory;
			_tfReaderFactory = tfReaderFactory;
			_fileNameProvider = new GuidFilenameProvider(directory);
			_maxSizeForMemory = maxSizeForMemory;
			_maxTablesPerLevel = maxTablesPerLevel;
			_additionalReclaim = additionalReclaim;
			_inMem = inMem;
			_skipIndexVerify = ShouldForceIndexVerify() ? false : skipIndexVerify;
			_indexCacheDepth = indexCacheDepth;
			_initializationThreads = initializationThreads;
			_ptableVersion = ptableVersion;
			_awaitingMemTables = new List<TableItem> {new TableItem(_memTableFactory(), -1, -1, 0)};

			_lowHasher = lowHasher;
			_highHasher = highHasher;

			_maxAutoMergeIndexLevel = maxAutoMergeIndexLevel;
			_pTableMaxReaderCount = pTableMaxReaderCount;
		}

		public void Initialize(long chaserCheckpoint) {
			Ensure.Nonnegative(chaserCheckpoint, "chaserCheckpoint");

			//NOT THREAD SAFE (assumes one thread)
			if (_initialized)
				throw new IOException("TableIndex is already initialized.");
			_initialized = true;

			if (_inMem) {
				_indexMap = IndexMap.CreateEmpty(_maxTablesPerLevel, int.MaxValue, _pTableMaxReaderCount);
				_prepareCheckpoint = _indexMap.PrepareCheckpoint;
				_commitCheckpoint = _indexMap.CommitCheckpoint;
				return;
			}

			if (ShouldForceIndexVerify()) {
				Log.Debug("Forcing verification of index files...");
			}

			CreateIfDoesNotExist(_directory);
			var indexmapFile = Path.Combine(_directory, IndexMapFilename);

			// if TableIndex's CommitCheckpoint is >= amount of written TFChunk data,
			// we'll have to remove some of PTables as they point to non-existent data
			// this can happen (very unlikely, though) on master crash
			try {
				_indexMap = IndexMap.FromFile(indexmapFile, _maxTablesPerLevel, true, _indexCacheDepth,
					_skipIndexVerify, _initializationThreads, _maxAutoMergeIndexLevel, _pTableMaxReaderCount);
				if (_indexMap.CommitCheckpoint >= chaserCheckpoint) {
					_indexMap.Dispose(TimeSpan.FromMilliseconds(5000));
					throw new CorruptIndexException(String.Format(
						"IndexMap's CommitCheckpoint ({0}) is greater than ChaserCheckpoint ({1}).",
						_indexMap.CommitCheckpoint, chaserCheckpoint));
				}

				//verification should be completed by now
				DeleteForceIndexVerifyFile();
			} catch (CorruptIndexException exc) {
				Log.ErrorException(exc, "ReadIndex is corrupted...");
				LogIndexMapContent(indexmapFile);
				DumpAndCopyIndex();
				File.SetAttributes(indexmapFile, FileAttributes.Normal);
				File.Delete(indexmapFile);
				DeleteForceIndexVerifyFile();
				_indexMap = IndexMap.FromFile(indexmapFile, _maxTablesPerLevel, true, _indexCacheDepth,
					_skipIndexVerify, _initializationThreads, _maxAutoMergeIndexLevel, _pTableMaxReaderCount);
			}

			_prepareCheckpoint = _indexMap.PrepareCheckpoint;
			_commitCheckpoint = _indexMap.CommitCheckpoint;

			// clean up all other remaining files
			var indexFiles = _indexMap.InOrder().Select(x => Path.GetFileName(x.Filename))
				.Union(new[] {IndexMapFilename});
			var toDeleteFiles = Directory.EnumerateFiles(_directory).Select(Path.GetFileName)
				.Except(indexFiles, StringComparer.OrdinalIgnoreCase);
			foreach (var filePath in toDeleteFiles) {
				var file = Path.Combine(_directory, filePath);
				File.SetAttributes(file, FileAttributes.Normal);
				File.Delete(file);
			}
		}

		private static void LogIndexMapContent(string indexmapFile) {
			try {
				Log.Error("IndexMap '{indexMap}' content:\n {content}", indexmapFile,
					Helper.FormatBinaryDump(File.ReadAllBytes(indexmapFile)));
			} catch (Exception exc) {
				Log.ErrorException(exc, "Unexpected error while dumping IndexMap '{indexMap}'.", indexmapFile);
			}
		}

		private void DumpAndCopyIndex() {
			string dumpPath = null;
			try {
				dumpPath = Path.Combine(Path.GetDirectoryName(_directory),
					string.Format("index-backup-{0:yyyy-MM-dd_HH-mm-ss.fff}", DateTime.UtcNow));
				Log.Error("Making backup of index folder for inspection to {dumpPath}...", dumpPath);
				FileUtils.DirectoryCopy(_directory, dumpPath, copySubDirs: true);
			} catch (Exception exc) {
				Log.ErrorException(exc, "Unexpected error while copying index to backup dir '{dumpPath}'", dumpPath);
			}
		}

		private static void CreateIfDoesNotExist(string directory) {
			if (!Directory.Exists(directory))
				Directory.CreateDirectory(directory);
		}

		public void Add(long commitPos, string streamId, long version, long position) {
			Ensure.Nonnegative(commitPos, "commitPos");
			Ensure.Nonnegative(version, "version");
			Ensure.Nonnegative(position, "position");

			AddEntries(commitPos, new[] {CreateIndexKey(streamId, version, position)});
		}

		public void AddEntries(long commitPos, IList<IndexKey> entries) {
			//should only be called on a single thread.
			var table = (IMemTable)_awaitingMemTables[0].Table; // always a memtable

			var collection = entries.Select(x => CreateIndexEntry(x)).ToList();
			table.AddEntries(collection);

			if (table.Count >= _maxSizeForMemory) {
				long prepareCheckpoint = collection[0].Position;
				for (int i = 1, n = collection.Count; i < n; ++i) {
					prepareCheckpoint = Math.Max(prepareCheckpoint, collection[i].Position);
				}

				TryProcessAwaitingTables(commitPos, prepareCheckpoint);
			}
		}

		public Task MergeIndexes() {
			TryManualMerge();
			return Task.CompletedTask;
		}

		public bool IsBackgroundTaskRunning {
			get { return _backgroundRunning; }
		}

		//Automerge only
		private void TryProcessAwaitingTables(long commitPos, long prepareCheckpoint) {
			lock (_awaitingTablesLock) {
				var newTables = new List<TableItem> {new TableItem(_memTableFactory(), -1, -1, 0)};
				newTables.AddRange(_awaitingMemTables.Select(
					(x, i) => i == 0 ? new TableItem(x.Table, prepareCheckpoint, commitPos, x.Level) : x));

				Log.Trace("Switching MemTable, currently: {awaitingMemTables} awaiting tables.", newTables.Count);

				_awaitingMemTables = newTables;
				if (_inMem) return;
				TryProcessAwaitingTables();

				if (_additionalReclaim)
					ThreadPool.QueueUserWorkItem(x => ReclaimMemoryIfNeeded(_awaitingMemTables));
			}
		}

		public void TryManualMerge() {
			lock (_awaitingTablesLock) {
				var (maxLevel, highest) = _indexMap.GetTableForManualMerge();
				if (highest == null) return; //no work to do

				//These values are actually ignored later as manual merge will never change the checkpoint as no
				//new entries are added, but they can be helpful to see when the manual merge was called
				//because of the way the "queue" currently works (LIFO) it should always be the same
				var prepare = _indexMap.PrepareCheckpoint;
				var commit = _indexMap.CommitCheckpoint;
				var newTables = new List<TableItem>(_awaitingMemTables) {
					new TableItem(highest, prepare, commit, maxLevel)
				};
				_awaitingMemTables = newTables;
				TryProcessAwaitingTables();
			}
		}

		private void TryProcessAwaitingTables() {
			lock (_awaitingTablesLock) {
				if (!_backgroundRunning) {
					_backgroundRunningEvent.Reset();
					_backgroundRunning = true;
					ThreadPool.QueueUserWorkItem(x => ReadOffQueue());
				}
			}
		}

		private void ReadOffQueue() {
			try {
				while (true) {
					TableItem tableItem;
					//ISearchTable table;
					lock (_awaitingTablesLock) {
						Log.Trace("Awaiting tables queue size is: {awaitingMemTables}.", _awaitingMemTables.Count);
						if (_awaitingMemTables.Count == 1) {
							return;
						}

						tableItem = _awaitingMemTables[_awaitingMemTables.Count - 1];
					}

					PTable ptable;
					var memtable = tableItem.Table as IMemTable;
					if (memtable != null) {
						memtable.MarkForConversion();
						ptable = PTable.FromMemtable(memtable, _fileNameProvider.GetFilenameNewTable(),
							ESConsts.PTableInitialReaderCount,
							_pTableMaxReaderCount,
							_indexCacheDepth, _skipIndexVerify);
					} else
						ptable = (PTable)tableItem.Table;

					var indexmapFile = Path.Combine(_directory, IndexMapFilename);
					MergeResult mergeResult;
					using (var reader = _tfReaderFactory()) {
						mergeResult = _indexMap.AddPTable(ptable, tableItem.PrepareCheckpoint,
							tableItem.CommitCheckpoint,
							(streamId, currentHash) => UpgradeHash(streamId, currentHash),
							entry => reader.ExistsAt(entry.Position),
							entry => ReadEntry(reader, entry.Position),
							_fileNameProvider,
							_ptableVersion,
							tableItem.Level,
							_indexCacheDepth,
							_skipIndexVerify);
					}

					_indexMap = mergeResult.MergedMap;
					_indexMap.SaveToFile(indexmapFile);

					lock (_awaitingTablesLock) {
						var memTables = _awaitingMemTables.ToList();

						var corrTable = memTables.First(x => x.Table.Id == ptable.Id);
						memTables.Remove(corrTable);

						// parallel thread could already switch table,
						// so if we have another PTable instance with same ID,
						// we need to kill that instance as we added ours already
						if (!ReferenceEquals(corrTable.Table, ptable) && corrTable.Table is PTable)
							((PTable)corrTable.Table).MarkForDestruction();

						Log.Trace("There are now {awaitingMemTables} awaiting tables.", memTables.Count);
						_awaitingMemTables = memTables;
					}

					mergeResult.ToDelete.ForEach(x => x.MarkForDestruction());
				}
			} catch (FileBeingDeletedException exc) {
				Log.ErrorException(exc,
					"Could not acquire chunk in TableIndex.ReadOffQueue. It is OK if node is shutting down.");
			} catch (Exception exc) {
				Log.ErrorException(exc, "Error in TableIndex.ReadOffQueue");
				throw;
			} finally {
				lock (_awaitingTablesLock) {
					_backgroundRunning = false;
					_backgroundRunningEvent.Set();
				}
			}
		}

		internal void WaitForBackgroundTasks() {
			if (!_backgroundRunningEvent.Wait(7000)) {
				throw new TimeoutException("Waiting for background tasks took too long.");
			}
		}

		public void Scavenge(IIndexScavengerLog log, CancellationToken ct) {
			GetExclusiveBackgroundTask(ct);
			var sw = Stopwatch.StartNew();

			try {
				Log.Info("Starting scavenge of TableIndex.");
				ScavengeInternal(log, ct);
			} finally {
				// Since scavenging indexes is the only place the ExistsAt optimization makes sense (and takes up a lot of memory), we can clear it after an index scavenge has completed. 
				TFChunkReaderExistsAtOptimizer.Instance.DeOptimizeAll();

				lock (_awaitingTablesLock) {
					_backgroundRunning = false;
					_backgroundRunningEvent.Set();

					TryProcessAwaitingTables();
				}

				Log.Info("Completed scavenge of TableIndex.  Elapsed: {elapsed}", sw.Elapsed);
			}
		}

		private void ScavengeInternal(IIndexScavengerLog log, CancellationToken ct) {
			var toScavenge = _indexMap.InOrder().ToList();

			foreach (var pTable in toScavenge) {
				var startNew = Stopwatch.StartNew();

				try {
					ct.ThrowIfCancellationRequested();

					using (var reader = _tfReaderFactory()) {
						var indexmapFile = Path.Combine(_directory, IndexMapFilename);

						var scavengeResult = _indexMap.Scavenge(pTable.Id, ct,
							(streamId, currentHash) => UpgradeHash(streamId, currentHash),
							entry => reader.ExistsAt(entry.Position),
							entry => ReadEntry(reader, entry.Position), _fileNameProvider, _ptableVersion,
							_indexCacheDepth, _skipIndexVerify);

						if (scavengeResult.IsSuccess) {
							_indexMap = scavengeResult.ScavengedMap;
							_indexMap.SaveToFile(indexmapFile);

							scavengeResult.OldTable.MarkForDestruction();

							var entriesDeleted = scavengeResult.OldTable.Count - scavengeResult.NewTable.Count;
							log.IndexTableScavenged(scavengeResult.Level, scavengeResult.Index, startNew.Elapsed,
								entriesDeleted, scavengeResult.NewTable.Count, scavengeResult.SpaceSaved);
						} else {
							log.IndexTableNotScavenged(scavengeResult.Level, scavengeResult.Index, startNew.Elapsed,
								pTable.Count, "");
						}
					}
				} catch (OperationCanceledException) {
					log.IndexTableNotScavenged(-1, -1, startNew.Elapsed, pTable.Count, "Scavenge cancelled");
					throw;
				} catch (Exception ex) {
					log.IndexTableNotScavenged(-1, -1, startNew.Elapsed, pTable.Count, ex.Message);
					throw;
				}
			}
		}

		private void GetExclusiveBackgroundTask(CancellationToken ct) {
			while (true) {
				lock (_awaitingTablesLock) {
					if (!_backgroundRunning) {
						_backgroundRunningEvent.Reset();
						_backgroundRunning = true;
						return;
					}
				}

				Log.Info("Waiting for TableIndex background task to complete before starting scavenge.");
				_backgroundRunningEvent.Wait(ct);
			}
		}

		private static Tuple<string, bool> ReadEntry(TFReaderLease reader, long position) {
			RecordReadResult result = reader.TryReadAt(position);
			if (!result.Success)
				return new Tuple<string, bool>(String.Empty, false);
			if (result.LogRecord.RecordType != TransactionLog.LogRecords.LogRecordType.Prepare)
				throw new Exception(string.Format("Incorrect type of log record {0}, expected Prepare record.",
					result.LogRecord.RecordType));
			return new Tuple<string, bool>(((TransactionLog.LogRecords.PrepareLogRecord)result.LogRecord).EventStreamId,
				true);
		}

		private void ReclaimMemoryIfNeeded(List<TableItem> awaitingMemTables) {
			var toPutOnDisk = awaitingMemTables.OfType<IMemTable>().Count() - MaxMemoryTables;
			for (var i = awaitingMemTables.Count - 1; i >= 1 && toPutOnDisk > 0; i--) {
				var memtable = awaitingMemTables[i].Table as IMemTable;
				if (memtable == null || !memtable.MarkForConversion())
					continue;

				Log.Trace("Putting awaiting file as PTable instead of MemTable [{id}].", memtable.Id);

				var ptable = PTable.FromMemtable(memtable, _fileNameProvider.GetFilenameNewTable(),
					ESConsts.PTableInitialReaderCount,
					_pTableMaxReaderCount,
					_indexCacheDepth,
					_skipIndexVerify);
				var swapped = false;
				lock (_awaitingTablesLock) {
					for (var j = _awaitingMemTables.Count - 1; j >= 1; j--) {
						var tableItem = _awaitingMemTables[j];
						if (!(tableItem.Table is IMemTable) || tableItem.Table.Id != ptable.Id) continue;
						swapped = true;
						_awaitingMemTables[j] = new TableItem(ptable,
							tableItem.PrepareCheckpoint,
							tableItem.CommitCheckpoint,
							tableItem.Level);
						break;
					}
				}

				if (!swapped)
					ptable.MarkForDestruction();
				toPutOnDisk--;
			}
		}

		public bool TryGetOneValue(string streamId, long version, out long position) {
			ulong stream = CreateHash(streamId);
			int counter = 0;
			while (counter < 5) {
				counter++;
				try {
					return TryGetOneValueInternal(stream, version, out position);
				} catch (FileBeingDeletedException) {
					Log.Trace("File being deleted.");
				} catch (MaybeCorruptIndexException e) {
					ForceIndexVerifyOnNextStartup();
					throw e;
				}
			}

			throw new InvalidOperationException("Files are locked.");
		}

		private bool TryGetOneValueInternal(ulong stream, long version, out long position) {
			if (version < 0)
				throw new ArgumentOutOfRangeException("version");

			var awaiting = _awaitingMemTables;
			foreach (var tableItem in awaiting) {
				if (tableItem.Table.TryGetOneValue(stream, version, out position))
					return true;
			}

			var map = _indexMap;
			foreach (var table in map.InOrder()) {
				if (table.TryGetOneValue(stream, version, out position))
					return true;
			}

			position = 0;
			return false;
		}

		public bool TryGetLatestEntry(string streamId, out IndexEntry entry) {
			ulong stream = CreateHash(streamId);
			var counter = 0;
			while (counter < 5) {
				counter++;
				try {
					return TryGetLatestEntryInternal(stream, out entry);
				} catch (FileBeingDeletedException) {
					Log.Trace("File being deleted.");
				} catch (MaybeCorruptIndexException e) {
					ForceIndexVerifyOnNextStartup();
					throw e;
				}
			}

			throw new InvalidOperationException("Files are locked.");
		}

		private bool TryGetLatestEntryInternal(ulong stream, out IndexEntry entry) {
			var awaiting = _awaitingMemTables;
			foreach (var t in awaiting) {
				if (t.Table.TryGetLatestEntry(stream, out entry))
					return true;
			}

			var map = _indexMap;
			foreach (var table in map.InOrder()) {
				if (table.TryGetLatestEntry(stream, out entry))
					return true;
			}

			entry = InvalidIndexEntry;
			return false;
		}

		public bool TryGetOldestEntry(string streamId, out IndexEntry entry) {
			ulong stream = CreateHash(streamId);
			var counter = 0;
			while (counter < 5) {
				counter++;
				try {
					return TryGetOldestEntryInternal(stream, out entry);
				} catch (FileBeingDeletedException) {
					Log.Trace("File being deleted.");
				} catch (MaybeCorruptIndexException e) {
					ForceIndexVerifyOnNextStartup();
					throw e;
				}
			}

			throw new InvalidOperationException("Files are locked.");
		}

		private bool TryGetOldestEntryInternal(ulong stream, out IndexEntry entry) {
			var map = _indexMap;
			foreach (var table in map.InReverseOrder()) {
				if (table.TryGetOldestEntry(stream, out entry))
					return true;
			}

			var awaiting = _awaitingMemTables;
			for (var index = awaiting.Count - 1; index >= 0; index--) {
				if (awaiting[index].Table.TryGetOldestEntry(stream, out entry))
					return true;
			}

			entry = InvalidIndexEntry;
			return false;
		}

		public IEnumerable<IndexEntry> GetRange(string streamId, long startVersion, long endVersion,
			int? limit = null) {
			ulong hash = CreateHash(streamId);
			var counter = 0;
			while (counter < 5) {
				counter++;
				try {
					return GetRangeInternal(hash, startVersion, endVersion, limit);
				} catch (FileBeingDeletedException) {
					Log.Trace("File being deleted.");
				} catch (MaybeCorruptIndexException e) {
					ForceIndexVerifyOnNextStartup();
					throw e;
				}
			}

			throw new InvalidOperationException("Files are locked.");
		}

		private IEnumerable<IndexEntry> GetRangeInternal(ulong hash, long startVersion, long endVersion,
			int? limit = null) {
			if (startVersion < 0)
				throw new ArgumentOutOfRangeException("startVersion");
			if (endVersion < 0)
				throw new ArgumentOutOfRangeException("endVersion");

			var candidates = new List<IEnumerator<IndexEntry>>();

			var awaiting = _awaitingMemTables;
			for (int index = 0; index < awaiting.Count; index++) {
				var range = awaiting[index].Table.GetRange(hash, startVersion, endVersion, limit).GetEnumerator();
				if (range.MoveNext())
					candidates.Add(range);
			}

			var map = _indexMap;
			foreach (var table in map.InOrder()) {
				var range = table.GetRange(hash, startVersion, endVersion, limit).GetEnumerator();
				if (range.MoveNext())
					candidates.Add(range);
			}

			var last = new IndexEntry(0, 0, 0);
			var first = true;

			var sortedCandidates = new List<IndexEntry>();
			while (candidates.Count > 0) {
				var maxIdx = GetMaxOf(candidates);
				var winner = candidates[maxIdx];

				var best = winner.Current;
				if (first || ((last.Stream != best.Stream) && (last.Version != best.Version)) ||
				    last.Position != best.Position) {
					last = best;
					sortedCandidates.Add(best);
					first = false;
				}

				if (!winner.MoveNext())
					candidates.RemoveAt(maxIdx);
			}

			return sortedCandidates;
		}

		private static int GetMaxOf(List<IEnumerator<IndexEntry>> enumerators) {
			var max = new IndexEntry(ulong.MinValue, 0, long.MinValue);
			int idx = 0;
			for (int i = 0; i < enumerators.Count; i++) {
				var cur = enumerators[i].Current;
				if (cur.CompareTo(max) > 0) {
					max = cur;
					idx = i;
				}
			}

			return idx;
		}

		public void Close(bool removeFiles = true) {
			if (!_backgroundRunningEvent.Wait(7000))
				throw new TimeoutException("Could not finish background thread in reasonable time.");
			if (_inMem)
				return;
			if (_indexMap == null) return;
			if (removeFiles) {
				_indexMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
				var fileName = Path.Combine(_directory, IndexMapFilename);
				if (File.Exists(fileName)) {
					File.SetAttributes(fileName, FileAttributes.Normal);
					File.Delete(fileName);
				}
			} else {
				_indexMap.InOrder().ToList().ForEach(x => x.Dispose());
			}

			_indexMap.InOrder().ToList().ForEach(x => x.WaitForDisposal(TimeSpan.FromMilliseconds(5000)));
		}

		private IndexEntry CreateIndexEntry(IndexKey key) {
			key = CreateIndexKey(key.StreamId, key.Version, key.Position);
			return new IndexEntry(key.Hash, key.Version, key.Position);
		}

		private ulong UpgradeHash(string streamId, ulong lowHash) {
			return lowHash << 32 | _highHasher.Hash(streamId);
		}

		private ulong CreateHash(string streamId) {
			return (ulong)_lowHasher.Hash(streamId) << 32 | _highHasher.Hash(streamId);
		}

		private IndexKey CreateIndexKey(string streamId, long version, long position) {
			return new IndexKey(streamId, version, position, CreateHash(streamId));
		}

		private class TableItem {
			public readonly ISearchTable Table;
			public readonly long PrepareCheckpoint;
			public readonly long CommitCheckpoint;
			public readonly int Level;

			public TableItem(ISearchTable table, long prepareCheckpoint, long commitCheckpoint, int level) {
				Table = table;
				PrepareCheckpoint = prepareCheckpoint;
				CommitCheckpoint = commitCheckpoint;
				Level = level;
			}
		}

		private void ForceIndexVerifyOnNextStartup() {
			Log.Debug("Forcing index verification on next startup");
			string path = Path.Combine(_directory, ForceIndexVerifyFilename);
			try {
				using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate)) {
				}

				;
			} catch {
				Log.Error("Could not create force index verification file at: {path}", path);
			}

			return;
		}

		private bool ShouldForceIndexVerify() {
			string path = Path.Combine(_directory, ForceIndexVerifyFilename);
			return File.Exists(path);
		}

		private void DeleteForceIndexVerifyFile() {
			string path = Path.Combine(_directory, ForceIndexVerifyFilename);
			try {
				if (File.Exists(path)) {
					File.SetAttributes(path, FileAttributes.Normal);
					File.Delete(path);
				}
			} catch {
				Log.Error("Could not delete force index verification file at: {path}", path);
			}
		}
	}
}
