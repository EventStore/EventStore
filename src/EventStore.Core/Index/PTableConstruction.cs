using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.Core.Index {
	public unsafe partial class PTable {
		public static PTable FromFile(string filename, int cacheDepth, bool skipIndexVerify) {
			return new PTable(filename, Guid.NewGuid(), depth: cacheDepth, skipIndexVerify: skipIndexVerify);
		}

		public static PTable FromMemtable(IMemTable table, string filename, int cacheDepth = 16,
			bool skipIndexVerify = false) {
			Ensure.NotNull(table, "table");
			Ensure.NotNullOrEmpty(filename, "filename");
			Ensure.Nonnegative(cacheDepth, "cacheDepth");

			int indexEntrySize = GetIndexEntrySize(table.Version);
			long dumpedEntryCount = 0;

			var sw = Stopwatch.StartNew();
			using (var fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
				DefaultSequentialBufferSize, FileOptions.SequentialScan)) {
				var fileSize = GetFileSizeUpToIndexEntries(table.Count, table.Version);
				fs.SetLength(fileSize);
				fs.Seek(0, SeekOrigin.Begin);

				using (var md5 = MD5.Create())
				using (var cs = new CryptoStream(fs, md5, CryptoStreamMode.Write))
				using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize)) {
					// WRITE HEADER
					var headerBytes = new PTableHeader(table.Version).AsByteArray();
					cs.Write(headerBytes, 0, headerBytes.Length);

					// WRITE INDEX ENTRIES
					var buffer = new byte[indexEntrySize];
					var records = table.IterateAllInOrder();
					List<Midpoint> midpoints = new List<Midpoint>();
					var requiredMidpointCount = GetRequiredMidpointCountCached(table.Count, table.Version, cacheDepth);

					long indexEntry = 0L;
					foreach (var rec in records) {
						AppendRecordTo(bs, buffer, table.Version, rec, indexEntrySize);
						dumpedEntryCount += 1;
						if (table.Version >= PTableVersions.IndexV4 &&
						    IsMidpointIndex(indexEntry, table.Count, requiredMidpointCount)) {
							midpoints.Add(new Midpoint(new IndexEntryKey(rec.Stream, rec.Version), indexEntry));
						}

						indexEntry++;
					}

					//WRITE MIDPOINTS
					if (table.Version >= PTableVersions.IndexV4) {
						var numIndexEntries = table.Count;
						if (dumpedEntryCount != numIndexEntries) {
							//if index entries have been removed, compute the midpoints again
							numIndexEntries = dumpedEntryCount;
							requiredMidpointCount =
								GetRequiredMidpointCount(numIndexEntries, table.Version, cacheDepth);
							midpoints = ComputeMidpoints(bs, fs, table.Version, indexEntrySize, numIndexEntries,
								requiredMidpointCount, midpoints);
						}

						WriteMidpointsTo(bs, fs, table.Version, indexEntrySize, buffer, dumpedEntryCount,
							numIndexEntries, requiredMidpointCount, midpoints);
					}

					bs.Flush();
					cs.FlushFinalBlock();

					// WRITE MD5
					var hash = md5.Hash;
					fs.SetLength(fs.Position + MD5Size);
					fs.Write(hash, 0, hash.Length);
					fs.FlushToDisk();
				}
			}

			Log.Trace("Dumped MemTable [{id}, {table} entries] in {elapsed}.", table.Id, table.Count, sw.Elapsed);
			return new PTable(filename, table.Id, depth: cacheDepth, skipIndexVerify: skipIndexVerify);
		}

		public static PTable MergeTo(IList<PTable> tables, string outputFile, Func<string, ulong, ulong> upgradeHash,
			Func<IndexEntry, bool> existsAt, Func<IndexEntry, Tuple<string, bool>> readRecord, byte version,
			int cacheDepth = 16, bool skipIndexVerify = false) {
			Ensure.NotNull(tables, "tables");
			Ensure.NotNullOrEmpty(outputFile, "outputFile");
			Ensure.Nonnegative(cacheDepth, "cacheDepth");

			var indexEntrySize = GetIndexEntrySize(version);

			long numIndexEntries = 0;
			for (var i = 0; i < tables.Count; i++)
				numIndexEntries += tables[i].Count;

			var fileSizeUpToIndexEntries = GetFileSizeUpToIndexEntries(numIndexEntries, version);
			if (tables.Count == 2)
				return MergeTo2(tables, numIndexEntries, indexEntrySize, outputFile, upgradeHash, existsAt, readRecord,
					version, cacheDepth, skipIndexVerify); // special case

			Log.Trace("PTables merge started.");
			var watch = Stopwatch.StartNew();

			var enumerators = tables
				.Select(table => new EnumerableTable(version, table, upgradeHash, existsAt, readRecord)).ToList();
			try {
				for (int i = 0; i < enumerators.Count; i++) {
					if (!enumerators[i].MoveNext()) {
						enumerators[i].Dispose();
						enumerators.RemoveAt(i);
						i--;
					}
				}

				long dumpedEntryCount = 0;
				using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
					DefaultSequentialBufferSize, FileOptions.SequentialScan)) {
					f.SetLength(fileSizeUpToIndexEntries);
					f.Seek(0, SeekOrigin.Begin);

					using (var md5 = MD5.Create())
					using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
					using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize)) {
						// WRITE HEADER
						var headerBytes = new PTableHeader(version).AsByteArray();
						cs.Write(headerBytes, 0, headerBytes.Length);

						var buffer = new byte[indexEntrySize];
						long indexEntry = 0L;
						List<Midpoint> midpoints = new List<Midpoint>();
						var requiredMidpointCount =
							GetRequiredMidpointCountCached(numIndexEntries, version, cacheDepth);
						// WRITE INDEX ENTRIES
						while (enumerators.Count > 0) {
							var idx = GetMaxOf(enumerators);
							var current = enumerators[idx].Current;
							AppendRecordTo(bs, buffer, version, current, indexEntrySize);
							if (version >= PTableVersions.IndexV4 &&
							    IsMidpointIndex(indexEntry, numIndexEntries, requiredMidpointCount)) {
								midpoints.Add(new Midpoint(new IndexEntryKey(current.Stream, current.Version),
									indexEntry));
							}

							indexEntry++;
							dumpedEntryCount++;

							if (!enumerators[idx].MoveNext()) {
								enumerators[idx].Dispose();
								enumerators.RemoveAt(idx);
							}
						}

						//WRITE MIDPOINTS
						if (version >= PTableVersions.IndexV4) {
							if (dumpedEntryCount != numIndexEntries) {
								//if index entries have been removed, compute the midpoints again
								numIndexEntries = dumpedEntryCount;
								requiredMidpointCount = GetRequiredMidpointCount(numIndexEntries, version, cacheDepth);
								midpoints = ComputeMidpoints(bs, f, version, indexEntrySize, numIndexEntries,
									requiredMidpointCount, midpoints);
							}

							WriteMidpointsTo(bs, f, version, indexEntrySize, buffer, dumpedEntryCount, numIndexEntries,
								requiredMidpointCount, midpoints);
						}

						bs.Flush();
						cs.FlushFinalBlock();

						f.FlushToDisk();
						f.SetLength(f.Position + MD5Size);

						// WRITE MD5
						var hash = md5.Hash;
						f.Write(hash, 0, hash.Length);
						f.FlushToDisk();
					}
				}

				Log.Trace(
					"PTables merge finished in {elapsed} ([{entryCount}] entries merged into {dumpedEntryCount}).",
					watch.Elapsed, string.Join(", ", tables.Select(x => x.Count)), dumpedEntryCount);
				return new PTable(outputFile, Guid.NewGuid(), depth: cacheDepth, skipIndexVerify: skipIndexVerify);
			} finally {
				foreach (var enumerableTable in enumerators) {
					enumerableTable.Dispose();
				}
			}
		}

		private static int GetIndexEntrySize(byte version) {
			if (version == PTableVersions.IndexV1) {
				return PTable.IndexEntryV1Size;
			}

			if (version == PTableVersions.IndexV2) {
				return PTable.IndexEntryV2Size;
			}

			if (version == PTableVersions.IndexV3) {
				return PTable.IndexEntryV3Size;
			}

			return PTable.IndexEntryV4Size;
		}

		private static PTable MergeTo2(IList<PTable> tables, long numIndexEntries, int indexEntrySize,
			string outputFile,
			Func<string, ulong, ulong> upgradeHash, Func<IndexEntry, bool> existsAt,
			Func<IndexEntry, Tuple<string, bool>> readRecord,
			byte version, int cacheDepth, bool skipIndexVerify) {
			Log.Trace("PTables merge started (specialized for <= 2 tables).");
			var watch = Stopwatch.StartNew();

			var fileSizeUpToIndexEntries = GetFileSizeUpToIndexEntries(numIndexEntries, version);
			var enumerators = tables
				.Select(table => new EnumerableTable(version, table, upgradeHash, existsAt, readRecord)).ToList();
			try {
				long dumpedEntryCount = 0;
				using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
					DefaultSequentialBufferSize, FileOptions.SequentialScan)) {
					f.SetLength(fileSizeUpToIndexEntries);
					f.Seek(0, SeekOrigin.Begin);

					using (var md5 = MD5.Create())
					using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
					using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize)) {
						// WRITE HEADER
						var headerBytes = new PTableHeader(version).AsByteArray();
						cs.Write(headerBytes, 0, headerBytes.Length);

						// WRITE INDEX ENTRIES
						var buffer = new byte[indexEntrySize];
						long indexEntry = 0L;
						List<Midpoint> midpoints = new List<Midpoint>();
						var requiredMidpointCount =
							GetRequiredMidpointCountCached(numIndexEntries, version, cacheDepth);
						var enum1 = enumerators[0];
						var enum2 = enumerators[1];
						bool available1 = enum1.MoveNext();
						bool available2 = enum2.MoveNext();
						IndexEntry current;
						while (available1 || available2) {
							var entry1 = new IndexEntry(enum1.Current.Stream, enum1.Current.Version,
								enum1.Current.Position);
							var entry2 = new IndexEntry(enum2.Current.Stream, enum2.Current.Version,
								enum2.Current.Position);

							if (available1 && (!available2 || entry1.CompareTo(entry2) > 0)) {
								current = entry1;
								available1 = enum1.MoveNext();
							} else {
								current = entry2;
								available2 = enum2.MoveNext();
							}

							AppendRecordTo(bs, buffer, version, current, indexEntrySize);
							if (version >= PTableVersions.IndexV4 &&
							    IsMidpointIndex(indexEntry, numIndexEntries, requiredMidpointCount)) {
								midpoints.Add(new Midpoint(new IndexEntryKey(current.Stream, current.Version),
									indexEntry));
							}

							indexEntry++;
							dumpedEntryCount++;
						}

						//WRITE MIDPOINTS
						if (version >= PTableVersions.IndexV4) {
							if (dumpedEntryCount != numIndexEntries) {
								//if index entries have been removed, compute the midpoints again
								numIndexEntries = dumpedEntryCount;
								requiredMidpointCount = GetRequiredMidpointCount(numIndexEntries, version, cacheDepth);
								midpoints = ComputeMidpoints(bs, f, version, indexEntrySize, numIndexEntries,
									requiredMidpointCount, midpoints);
							}

							WriteMidpointsTo(bs, f, version, indexEntrySize, buffer, dumpedEntryCount, numIndexEntries,
								requiredMidpointCount, midpoints);
						}

						bs.Flush();
						cs.FlushFinalBlock();

						f.SetLength(f.Position + MD5Size);

						// WRITE MD5
						var hash = md5.Hash;
						f.Write(hash, 0, hash.Length);
						f.FlushToDisk();
					}
				}

				Log.Trace(
					"PTables merge finished in {elapsed} ([{entryCount}] entries merged into {dumpedEntryCount}).",
					watch.Elapsed, string.Join(", ", tables.Select(x => x.Count)), dumpedEntryCount);
				return new PTable(outputFile, Guid.NewGuid(), depth: cacheDepth, skipIndexVerify: skipIndexVerify);
			} finally {
				foreach (var enumerator in enumerators) {
					enumerator.Dispose();
				}
			}
		}

		public static PTable Scavenged(PTable table, string outputFile, Func<string, ulong, ulong> upgradeHash,
			Func<IndexEntry, bool> existsAt, Func<IndexEntry, Tuple<string, bool>> readRecord, byte version,
			out long spaceSaved,
			int cacheDepth = 16, bool skipIndexVerify = false, CancellationToken ct = default(CancellationToken)) {
			Ensure.NotNull(table, "table");
			Ensure.NotNullOrEmpty(outputFile, "outputFile");
			Ensure.Nonnegative(cacheDepth, "cacheDepth");

			var indexEntrySize = GetIndexEntrySize(version);
			var numIndexEntries = table.Count;

			var fileSizeUpToIndexEntries = GetFileSizeUpToIndexEntries(numIndexEntries, version);

			Log.Trace("PTables scavenge started with {numIndexEntries} entries.", numIndexEntries);
			var watch = Stopwatch.StartNew();
			long keptCount = 0L;
			long droppedCount;

			try {
				using (var f = new FileStream(outputFile, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None,
					DefaultSequentialBufferSize, FileOptions.SequentialScan)) {
					f.SetLength(fileSizeUpToIndexEntries);
					f.Seek(0, SeekOrigin.Begin);

					using (var md5 = MD5.Create())
					using (var cs = new CryptoStream(f, md5, CryptoStreamMode.Write))
					using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize)) {
						// WRITE HEADER
						var headerBytes = new PTableHeader(version).AsByteArray();
						cs.Write(headerBytes, 0, headerBytes.Length);

						// WRITE SCAVENGED INDEX ENTRIES
						var buffer = new byte[indexEntrySize];
						using (var enumerator =
							new EnumerableTable(version, table, upgradeHash, existsAt, readRecord)) {
							while (enumerator.MoveNext()) {
								ct.ThrowIfCancellationRequested();
								if (existsAt(enumerator.Current)) {
									AppendRecordTo(bs, buffer, version, enumerator.Current, indexEntrySize);
									keptCount++;
								}
							}
						}

						// We calculate this as the EnumerableTable can silently drop entries too.
						droppedCount = numIndexEntries - keptCount;

						var forceKeep = version > table.Version;

						if (droppedCount == 0 && !forceKeep) {
							Log.Trace(
								"PTable scavenge finished in {elapsed}. No entries removed so not keeping scavenged table.",
								watch.Elapsed);

							try {
								bs.Close();
								File.Delete(outputFile);
							} catch (Exception ex) {
								Log.ErrorException(ex, "Unable to delete unwanted scavenged PTable: {outputFile}",
									outputFile);
							}

							spaceSaved = 0;
							return null;
						}

						if (droppedCount == 0 && forceKeep) {
							Log.Trace("Keeping scavenged index even though it isn't smaller; version upgraded.");
						}

						//CALCULATE AND WRITE MIDPOINTS
						if (version >= PTableVersions.IndexV4) {
							var requiredMidpointCount = GetRequiredMidpointCount(keptCount, version, cacheDepth);
							var midpoints = ComputeMidpoints(bs, f, version, indexEntrySize, keptCount,
								requiredMidpointCount, new List<Midpoint>(), ct);
							WriteMidpointsTo(bs, f, version, indexEntrySize, buffer, keptCount, keptCount,
								requiredMidpointCount, midpoints);
						}

						bs.Flush();
						cs.FlushFinalBlock();

						f.FlushToDisk();
						f.SetLength(f.Position + MD5Size);

						// WRITE MD5
						var hash = md5.Hash;
						f.Write(hash, 0, hash.Length);
						f.FlushToDisk();
					}
				}

				Log.Trace(
					"PTable scavenge finished in {elapsed} ({droppedCount} entries removed, {keptCount} remaining).",
					watch.Elapsed,
					droppedCount, keptCount);
				var scavengedTable = new PTable(outputFile, Guid.NewGuid(), depth: cacheDepth,
					skipIndexVerify: skipIndexVerify);
				spaceSaved = table._size - scavengedTable._size;
				return scavengedTable;
			} catch (Exception) {
				try {
					File.Delete(outputFile);
				} catch (Exception ex) {
					Log.ErrorException(ex, "Unable to delete unwanted scavenged PTable: {outputFile}", outputFile);
				}

				throw;
			}
		}

		private static int GetMaxOf(List<EnumerableTable> enumerators) {
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

		private static void AppendRecordTo(Stream stream, byte[] buffer, byte version, IndexEntry entry,
			int indexEntrySize) {
			var bytes = entry.Bytes;
			if (version == PTableVersions.IndexV1) {
				var entryV1 = new IndexEntryV1((uint)entry.Stream, (int)entry.Version, entry.Position);
				bytes = entryV1.Bytes;
			} else if (version == PTableVersions.IndexV2) {
				var entryV2 = new IndexEntryV2(entry.Stream, (int)entry.Version, entry.Position);
				bytes = entryV2.Bytes;
			}

			Marshal.Copy((IntPtr)bytes, buffer, 0, indexEntrySize);
			stream.Write(buffer, 0, indexEntrySize);
		}

		private static List<Midpoint> ComputeMidpoints(BufferedStream bs, FileStream fs, byte version,
			int indexEntrySize, long numIndexEntries, long requiredMidpointCount, List<Midpoint> midpoints,
			CancellationToken ct = default(CancellationToken)) {
			int indexKeySize;
			if (version == PTableVersions.IndexV4)
				indexKeySize = IndexKeyV4Size;
			else
				throw new InvalidOperationException("Unknown PTable version: " + version);

			midpoints.Clear();
			bs.Flush();
			byte[] buffer = new byte[indexKeySize];

			var previousFileStreamPosition = fs.Position;

			long previousIndex = -1;
			IndexEntryKey previousKey = new IndexEntryKey(0, 0);

			for (int k = 0; k < requiredMidpointCount; k++) {
				ct.ThrowIfCancellationRequested();

				long index = GetMidpointIndex(k, numIndexEntries, requiredMidpointCount);
				if (index == previousIndex) {
					midpoints.Add(new Midpoint(previousKey, previousIndex));
				} else {
					fs.Seek(PTableHeader.Size + index * indexEntrySize, SeekOrigin.Begin);
					fs.Read(buffer, 0, indexKeySize);
					IndexEntryKey key = new IndexEntryKey(BitConverter.ToUInt64(buffer, 8),
						BitConverter.ToInt64(buffer, 0));
					midpoints.Add(new Midpoint(key, index));
					previousIndex = index;
					previousKey = key;
				}
			}

			fs.Seek(previousFileStreamPosition, SeekOrigin.Begin);
			return midpoints;
		}

		private static void WriteMidpointsTo(BufferedStream bs, FileStream fs, byte version, int indexEntrySize,
			byte[] buffer, long dumpedEntryCount, long numIndexEntries, long requiredMidpointCount,
			List<Midpoint> midpoints) {
			//WRITE MIDPOINT ENTRIES

			//special case, when there is a single index entry, we need two midpoints
			if (numIndexEntries == 1 && midpoints.Count == 1) {
				midpoints.Add(new Midpoint(midpoints[0].Key, midpoints[0].ItemIndex));
			}

			var midpointsWritten = 0;
			if (dumpedEntryCount == numIndexEntries && requiredMidpointCount == midpoints.Count) {
				//if these values don't match, something is wrong
				bs.Flush();
				fs.SetLength(fs.Position + midpoints.Count * indexEntrySize);
				foreach (var pt in midpoints) {
					AppendMidpointRecordTo(bs, buffer, version, pt, indexEntrySize);
				}

				midpointsWritten = midpoints.Count;
				Log.Debug("Cached {midpointsWritten} index midpoints to PTable", midpointsWritten);
			} else
				Log.Debug(
					"Not caching index midpoints to PTable due to count mismatch. Table entries: {numIndexEntries} / Dumped entries: {dumpedEntryCount}, Required midpoint count: {requiredMidpointCount} /  Actual midpoint count: {midpoints}",
					numIndexEntries, dumpedEntryCount, requiredMidpointCount, midpoints.Count);

			bs.Flush();
			fs.SetLength(fs.Position + PTableFooter.GetSize(version));
			var footerBytes = new PTableFooter(version, (uint)midpointsWritten).AsByteArray();
			bs.Write(footerBytes, 0, footerBytes.Length);
			bs.Flush();
		}

		private static void AppendMidpointRecordTo(Stream stream, byte[] buffer, byte version, Midpoint midpointEntry,
			int midpointEntrySize) {
			if (version >= PTableVersions.IndexV4) {
				ulong eventStream = midpointEntry.Key.Stream;
				long eventVersion = midpointEntry.Key.Version;
				long itemIndex = midpointEntry.ItemIndex;

				for (int i = 0; i < 8; i++) {
					buffer[i] = (byte)(eventVersion & 0xFF);
					eventVersion >>= 8;
				}

				for (int i = 0; i < 8; i++) {
					buffer[i + 8] = (byte)(eventStream & 0xFF);
					eventStream >>= 8;
				}

				for (int i = 0; i < 8; i++) {
					buffer[i + 16] = (byte)(itemIndex & 0xFF);
					itemIndex >>= 8;
				}

				stream.Write(buffer, 0, midpointEntrySize);
			}
		}

		internal class EnumerableTable : IEnumerator<IndexEntry> {
			private ISearchTable _ptable;
			private List<IndexEntry> _list;
			private IEnumerator<IndexEntry> _enumerator;
			readonly IEnumerator<IndexEntry> _ptableEnumerator;
			private bool _firstIteration = true;
			private bool _lastIteration = false;

			readonly Func<string, ulong, ulong> _upgradeHash;
			readonly Func<IndexEntry, bool> _existsAt;
			readonly Func<IndexEntry, Tuple<string, bool>> _readRecord;
			readonly byte _mergedPTableVersion;
			static readonly IComparer<IndexEntry> EntryComparer = new IndexEntryComparer();

			public byte GetVersion() {
				return _ptable.Version;
			}

			public IndexEntry Current {
				get { return _enumerator.Current; }
			}

			object IEnumerator.Current {
				get { return _enumerator.Current; }
			}

			public EnumerableTable(byte mergedPTableVersion, ISearchTable table, Func<string, ulong, ulong> upgradeHash,
				Func<IndexEntry, bool> existsAt, Func<IndexEntry, Tuple<string, bool>> readRecord) {
				_mergedPTableVersion = mergedPTableVersion;
				_ptable = table;

				_upgradeHash = upgradeHash;
				_existsAt = existsAt;
				_readRecord = readRecord;

				if (table.Version == PTableVersions.IndexV1 && mergedPTableVersion != PTableVersions.IndexV1) {
					_list = new List<IndexEntry>();
					_enumerator = _list.GetEnumerator();
					_ptableEnumerator = _ptable.IterateAllInOrder().GetEnumerator();
				} else {
					_enumerator = _ptable.IterateAllInOrder().GetEnumerator();
				}
			}

			public void Dispose() {
				if (_ptableEnumerator != null) {
					_ptableEnumerator.Dispose();
				}

				_enumerator.Dispose();
			}

			public bool MoveNext() {
				var hasMovedToNext = _enumerator.MoveNext();
				if (_list == null || hasMovedToNext) return hasMovedToNext;

				_enumerator.Dispose();
				_list = ReadUntilDifferentHash(_mergedPTableVersion, _ptableEnumerator, _upgradeHash, _existsAt,
					_readRecord);
				_enumerator = _list.GetEnumerator();

				return _enumerator.MoveNext();
			}

			private List<IndexEntry> ReadUntilDifferentHash(byte version, IEnumerator<IndexEntry> ptableEnumerator,
				Func<string, ulong, ulong> upgradeHash, Func<IndexEntry, bool> existsAt,
				Func<IndexEntry, Tuple<string, bool>> readRecord) {
				var list = new List<IndexEntry>();

				if (_lastIteration)
					return list;

				//move to the next entry if it's the first iteration
				if (_firstIteration) {
					_firstIteration = false;
					if (!ptableEnumerator.MoveNext()) {
						_lastIteration = true;
						return list;
					}
				}

				//move until we find an index entry that exists
				while (!existsAt(ptableEnumerator.Current)) {
					if (!ptableEnumerator.MoveNext()) {
						_lastIteration = true;
						return list;
					}
				}

				//add index entries as long as the stream hashes match
				ulong hash = ptableEnumerator.Current.Stream;
				do {
					if (existsAt(ptableEnumerator.Current)) {
						var current = ptableEnumerator.Current;
						list.Add(new IndexEntry(upgradeHash(readRecord(current).Item1, current.Stream), current.Version,
							current.Position));
					}

					if (!ptableEnumerator.MoveNext()) {
						_lastIteration = true;
						break;
					}

					if (hash != ptableEnumerator.Current.Stream)
						break;
				} while (true);

				//sort the index entries with upgraded hashes
				list.Sort(EntryComparer);
				return list;
			}

			private class IndexEntryComparer : IComparer<IndexEntry> {
				public int Compare(IndexEntry x, IndexEntry y) {
					return -x.CompareTo(y);
				}
			}

			public void Reset() {
				_enumerator.Reset();
			}
		}

		private static long GetFileSizeUpToIndexEntries(long numIndexEntries, byte version) {
			int indexEntrySize = GetIndexEntrySize(version);
			return (long)PTableHeader.Size + numIndexEntries * indexEntrySize;
		}

		private static int GetDepth(long indexEntriesFileSize, int minDepth) {
			if ((2L << 28) * 4096L < indexEntriesFileSize) return 28;
			for (int i = 27; i > minDepth; i--) {
				if ((2L << i) * 4096L < indexEntriesFileSize) {
					return i + 1;
				}
			}

			return minDepth;
		}

		private static uint GetRequiredMidpointCount(long numIndexEntries, byte version, int minDepth) {
			if (numIndexEntries == 0) return 0;
			if (numIndexEntries == 1) return 2;

			int indexEntrySize = GetIndexEntrySize(version);
			var depth = GetDepth(numIndexEntries * indexEntrySize, minDepth);
			return (uint)Math.Max(2L, Math.Min((long)1 << depth, numIndexEntries));
		}


		public static uint GetRequiredMidpointCountCached(long numIndexEntries, byte version, int minDepth = 16) {
			if (version >= PTableVersions.IndexV4)
				return GetRequiredMidpointCount(numIndexEntries, version, minDepth);
			return 0;
		}

		public static long GetMidpointIndex(long k, long numIndexEntries, long numMidpoints) {
			if (numIndexEntries == 1 && numMidpoints == 2 && (k == 0 || k == 1)) return 0;
			return (long)k * (numIndexEntries - 1) / (numMidpoints - 1);
		}

		public static bool IsMidpointIndex(long index, long numIndexEntries, long numMidpoints) {
			//special cases
			if (numIndexEntries < 1) return false;
			if (numIndexEntries == 1) {
				if (numMidpoints == 2 && index == 0) return true;
				return false;
			}

			//a midpoint index entry satisfies:
			//index = floor (k * (numIndexEntries - 1) / (numMidpoints - 1));    for k = 0 to numMidpoints-1
			//we need to find if there exists an integer x, such that:
			//index*(numMidpoints-1)/(numIndexEntries-1) <= x < (index+1)*(numMidpoints-1)/(numIndexEntries-1)
			var lower = index * (numMidpoints - 1) / (numIndexEntries - 1);
			if ((index * (numMidpoints - 1)) % (numIndexEntries - 1) != 0) lower++;
			var upper = (index + 1) * (numMidpoints - 1) / (numIndexEntries - 1);
			if (((index + 1) * (numMidpoints - 1)) % (numIndexEntries - 1) == 0) upper--;
			return lower <= upper;
		}
	}
}
