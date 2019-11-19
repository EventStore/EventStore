using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Security.Cryptography;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Exceptions;
using EventStore.Core.Settings;
using EventStore.Core.TransactionLog.Unbuffered;

namespace EventStore.Core.Index {
	public enum FileType : byte {
		PTableFile = 1,
		ChunkFile = 2
	}

	public class PTableVersions {
		public const byte IndexV1 = 1;
		public const byte IndexV2 = 2;
		public const byte IndexV3 = 3;
		public const byte IndexV4 = 4;
	}

	public partial class PTable : ISearchTable, IDisposable {
		public const int IndexEntryV1Size = sizeof(int) + sizeof(int) + sizeof(long);
		public const int IndexEntryV2Size = sizeof(int) + sizeof(long) + sizeof(long);
		public const int IndexEntryV3Size = sizeof(long) + sizeof(long) + sizeof(long);
		public const int IndexEntryV4Size = IndexEntryV3Size;

		public const int IndexKeyV1Size = sizeof(int) + sizeof(int);
		public const int IndexKeyV2Size = sizeof(int) + sizeof(long);
		public const int IndexKeyV3Size = sizeof(long) + sizeof(long);
		public const int IndexKeyV4Size = IndexKeyV3Size;
		public const int MD5Size = 16;
		public const int DefaultBufferSize = 8192;
		public const int DefaultSequentialBufferSize = 65536;

		private static readonly ILogger Log = LogManager.GetLoggerFor<PTable>();

		public Guid Id {
			get { return _id; }
		}

		public long Count {
			get { return _count; }
		}

		public string Filename {
			get { return _filename; }
		}

		public byte Version {
			get { return _version; }
		}

		private readonly Guid _id;
		private readonly string _filename;
		private readonly long _count;
		private readonly long _size;
		private readonly Midpoint[] _midpoints;
		private readonly uint _midpointsCached = 0;
		private readonly long _midpointsCacheSize = 0;

		private readonly IndexEntryKey _minEntry, _maxEntry;
		private readonly ObjectPool<WorkItem> _workItems;
		private readonly byte _version;
		private readonly int _indexEntrySize;
		private readonly int _indexKeySize;

		private readonly ManualResetEventSlim _destroyEvent = new ManualResetEventSlim(false);
		private volatile bool _deleteFile;

		internal Midpoint[] GetMidPoints() {
			return _midpoints;
		}

		private PTable(string filename,
			Guid id,
			int initialReaders,
			int maxReaders,
			int depth = 16,
			bool skipIndexVerify = false) {
			Ensure.NotNullOrEmpty(filename, "filename");
			Ensure.NotEmptyGuid(id, "id");
			Ensure.Positive(maxReaders, "maxReaders");
			Ensure.Nonnegative(depth, "depth");

			if (!File.Exists(filename))
				throw new CorruptIndexException(new PTableNotFoundException(filename));

			_id = id;
			_filename = filename;

			Log.Trace("Loading " + (skipIndexVerify ? "" : "and Verification ") + "of PTable '{pTable}' started...",
				Path.GetFileName(Filename));
			var sw = Stopwatch.StartNew();
			_size = new FileInfo(_filename).Length;

			File.SetAttributes(_filename, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);

			_workItems = new ObjectPool<WorkItem>(string.Format("PTable {0} work items", _id),
				initialReaders,
				maxReaders,
				() => new WorkItem(filename, DefaultBufferSize),
				workItem => workItem.Dispose(),
				pool => OnAllWorkItemsDisposed());

			var readerWorkItem = GetWorkItem();
			try {
				readerWorkItem.Stream.Seek(0, SeekOrigin.Begin);
				var header = PTableHeader.FromStream(readerWorkItem.Stream);
				if ((header.Version != PTableVersions.IndexV1) &&
				    (header.Version != PTableVersions.IndexV2) &&
				    (header.Version != PTableVersions.IndexV3) &&
				    (header.Version != PTableVersions.IndexV4))
					throw new CorruptIndexException(new WrongFileVersionException(_filename, header.Version, Version));
				_version = header.Version;

				if (_version == PTableVersions.IndexV1) {
					_indexEntrySize = IndexEntryV1Size;
					_indexKeySize = IndexKeyV1Size;
				}

				if (_version == PTableVersions.IndexV2) {
					_indexEntrySize = IndexEntryV2Size;
					_indexKeySize = IndexKeyV2Size;
				}

				if (_version == PTableVersions.IndexV3) {
					_indexEntrySize = IndexEntryV3Size;
					_indexKeySize = IndexKeyV3Size;
				}

				if (_version >= PTableVersions.IndexV4) {
					//read the PTable footer
					var previousPosition = readerWorkItem.Stream.Position;
					readerWorkItem.Stream.Seek(readerWorkItem.Stream.Length - MD5Size - PTableFooter.GetSize(_version),
						SeekOrigin.Begin);
					var footer = PTableFooter.FromStream(readerWorkItem.Stream);
					if (footer.Version != header.Version)
						throw new CorruptIndexException(
							String.Format("PTable header/footer version mismatch: {0}/{1}", header.Version,
								footer.Version), new InvalidFileException("Invalid PTable file."));

					if (_version == PTableVersions.IndexV4) {
						_indexEntrySize = IndexEntryV4Size;
						_indexKeySize = IndexKeyV4Size;
					} else
						throw new InvalidOperationException("Unknown PTable version: " + _version);

					_midpointsCached = footer.NumMidpointsCached;
					_midpointsCacheSize = _midpointsCached * _indexEntrySize;
					readerWorkItem.Stream.Seek(previousPosition, SeekOrigin.Begin);
				}

				long indexEntriesTotalSize = (_size - PTableHeader.Size - _midpointsCacheSize -
				                              PTableFooter.GetSize(_version) - MD5Size);

				if (indexEntriesTotalSize < 0) {
					throw new CorruptIndexException(String.Format(
						"Total size of index entries < 0: {0}. _size: {1}, header size: {2}, _midpointsCacheSize: {3}, footer size: {4}, md5 size: {5}",
						indexEntriesTotalSize, _size, PTableHeader.Size, _midpointsCacheSize,
						PTableFooter.GetSize(_version), MD5Size));
				} else if (indexEntriesTotalSize % _indexEntrySize != 0) {
					throw new CorruptIndexException(String.Format(
						"Total size of index entries: {0} is not divisible by index entry size: {1}",
						indexEntriesTotalSize, _indexEntrySize));
				}

				_count = indexEntriesTotalSize / _indexEntrySize;

				if (_version >= PTableVersions.IndexV4 && _count > 0 && _midpointsCached > 0 && _midpointsCached < 2) {
					//if there is at least 1 index entry with version>=4 and there are cached midpoints, there should always be at least 2 midpoints cached
					throw new CorruptIndexException(String.Format(
						"Less than 2 midpoints cached in PTable. Index entries: {0}, Midpoints cached: {1}", _count,
						_midpointsCached));
				} else if (_count >= 2 && _midpointsCached > _count) {
					//if there are at least 2 index entries, midpoints count should be at most the number of index entries
					throw new CorruptIndexException(String.Format(
						"More midpoints cached in PTable than index entries. Midpoints: {0} , Index entries: {1}",
						_midpointsCached, _count));
				}

				if (Count == 0) {
					_minEntry = new IndexEntryKey(ulong.MaxValue, long.MaxValue);
					_maxEntry = new IndexEntryKey(ulong.MinValue, long.MinValue);
				} else {
					var minEntry = ReadEntry(_indexEntrySize, Count - 1, readerWorkItem, _version);
					_minEntry = new IndexEntryKey(minEntry.Stream, minEntry.Version);
					var maxEntry = ReadEntry(_indexEntrySize, 0, readerWorkItem, _version);
					_maxEntry = new IndexEntryKey(maxEntry.Stream, maxEntry.Version);
				}
			} catch (Exception) {
				Dispose();
				throw;
			} finally {
				ReturnWorkItem(readerWorkItem);
			}

			int calcdepth = 0;
			try {
				calcdepth = GetDepth(_count * _indexEntrySize, depth);
				_midpoints = CacheMidpointsAndVerifyHash(calcdepth, skipIndexVerify);
			} catch (PossibleToHandleOutOfMemoryException) {
				Log.Error(
					"Unable to create midpoints for PTable '{pTable}' ({count} entries, depth {depth} requested). "
					+ "Performance hit will occur. OOM Exception.", Path.GetFileName(Filename), Count, depth);
			}

			Log.Trace(
				"Loading PTable (Version: {version}) '{pTable}' ({count} entries, cache depth {depth}) done in {elapsed}.",
				_version, Path.GetFileName(Filename), Count, calcdepth, sw.Elapsed);
		}

		internal Midpoint[] CacheMidpointsAndVerifyHash(int depth, bool skipIndexVerify) {
			var buffer = new byte[4096];
			if (depth < 0 || depth > 30)
				throw new ArgumentOutOfRangeException("depth");
			var count = Count;
			if (count == 0 || depth == 0)
				return null;

			if (skipIndexVerify) {
				Log.Debug("Disabling Verification of PTable");
			}

			Stream stream = null;
			WorkItem workItem = null;
			if (Runtime.IsUnixOrMac) {
				workItem = GetWorkItem();
				stream = workItem.Stream;
			} else {
				stream = UnbufferedFileStream.Create(_filename, FileMode.Open, FileAccess.Read, FileShare.Read, false,
					4096, 4096, false, 4096);
			}

			try {
				int midpointsCount;
				Midpoint[] midpoints;
				using (MD5 md5 = MD5.Create()) {
					try {
						midpointsCount = (int)Math.Max(2L, Math.Min((long)1 << depth, count));
						midpoints = new Midpoint[midpointsCount];
					} catch (OutOfMemoryException exc) {
						throw new PossibleToHandleOutOfMemoryException("Failed to allocate memory for Midpoint cache.",
							exc);
					}

					if (skipIndexVerify && (_version >= PTableVersions.IndexV4)) {
						if (_midpointsCached == midpointsCount) {
							//index verification is disabled and cached midpoints with the same depth requested are available
							//so, we can load them directly from the PTable file
							Log.Debug("Loading {midpointsCached} cached midpoints from PTable", _midpointsCached);
							long startOffset = stream.Length - MD5Size - PTableFooter.GetSize(_version) -
							                   _midpointsCacheSize;
							stream.Seek(startOffset, SeekOrigin.Begin);
							for (uint k = 0; k < _midpointsCached; k++) {
								stream.Read(buffer, 0, _indexEntrySize);
								IndexEntryKey key;
								long index;
								if (_version == PTableVersions.IndexV4) {
									key = new IndexEntryKey(BitConverter.ToUInt64(buffer, 8),
										BitConverter.ToInt64(buffer, 0));
									index = BitConverter.ToInt64(buffer, 8 + 8);
								} else
									throw new InvalidOperationException("Unknown PTable version: " + _version);

								midpoints[k] = new Midpoint(key, index);

								if (k > 0) {
									if (midpoints[k].Key.GreaterThan(midpoints[k - 1].Key)) {
										throw new CorruptIndexException(String.Format(
											"Index entry key for midpoint {0} (stream: {1}, version: {2}) < index entry key for midpoint {3} (stream: {4}, version: {5})",
											k - 1, midpoints[k - 1].Key.Stream, midpoints[k - 1].Key.Version, k,
											midpoints[k].Key.Stream, midpoints[k].Key.Version));
									} else if (midpoints[k - 1].ItemIndex > midpoints[k].ItemIndex) {
										throw new CorruptIndexException(String.Format(
											"Item index for midpoint {0} ({1}) > Item index for midpoint {2} ({3})",
											k - 1, midpoints[k - 1].ItemIndex, k, midpoints[k].ItemIndex));
									}
								}
							}

							return midpoints;
						} else
							Log.Debug(
								"Skipping loading of cached midpoints from PTable due to count mismatch, cached midpoints: {midpointsCached} / required midpoints: {midpointsCount}",
								_midpointsCached, midpointsCount);
					}

					if (!skipIndexVerify) {
						stream.Seek(0, SeekOrigin.Begin);
						stream.Read(buffer, 0, PTableHeader.Size);
						md5.TransformBlock(buffer, 0, PTableHeader.Size, null, 0);
					}

					long previousNextIndex = long.MinValue;
					var previousKey = new IndexEntryKey(long.MaxValue, long.MaxValue);
					for (long k = 0; k < midpointsCount; ++k) {
						long nextIndex = GetMidpointIndex(k, count, midpointsCount);
						if (previousNextIndex != nextIndex) {
							if (!skipIndexVerify) {
								ReadUntilWithMd5(PTableHeader.Size + _indexEntrySize * nextIndex, stream, md5);
								stream.Read(buffer, 0, _indexKeySize);
								md5.TransformBlock(buffer, 0, _indexKeySize, null, 0);
							} else {
								stream.Seek(PTableHeader.Size + _indexEntrySize * nextIndex, SeekOrigin.Begin);
								stream.Read(buffer, 0, _indexKeySize);
							}

							IndexEntryKey key;
							if (_version == PTableVersions.IndexV1) {
								key = new IndexEntryKey(BitConverter.ToUInt32(buffer, 4),
									BitConverter.ToInt32(buffer, 0));
							} else if (_version == PTableVersions.IndexV2) {
								key = new IndexEntryKey(BitConverter.ToUInt64(buffer, 4),
									BitConverter.ToInt32(buffer, 0));
							} else {
								key = new IndexEntryKey(BitConverter.ToUInt64(buffer, 8),
									BitConverter.ToInt64(buffer, 0));
							}

							midpoints[k] = new Midpoint(key, nextIndex);
							previousNextIndex = nextIndex;
							previousKey = key;
						} else {
							midpoints[k] = new Midpoint(previousKey, previousNextIndex);
						}

						if (k > 0) {
							if (midpoints[k].Key.GreaterThan(midpoints[k - 1].Key)) {
								throw new CorruptIndexException(String.Format(
									"Index entry key for midpoint {0} (stream: {1}, version: {2}) < index entry key for midpoint {3} (stream: {4}, version: {5})",
									k - 1, midpoints[k - 1].Key.Stream, midpoints[k - 1].Key.Version, k,
									midpoints[k].Key.Stream, midpoints[k].Key.Version));
							} else if (midpoints[k - 1].ItemIndex > midpoints[k].ItemIndex) {
								throw new CorruptIndexException(String.Format(
									"Item index for midpoint {0} ({1}) > Item index for midpoint {2} ({3})", k - 1,
									midpoints[k - 1].ItemIndex, k, midpoints[k].ItemIndex));
							}
						}
					}

					if (!skipIndexVerify) {
						ReadUntilWithMd5(stream.Length - MD5Size, stream, md5);
						//verify hash (should be at stream.length - MD5Size)
						md5.TransformFinalBlock(Empty.ByteArray, 0, 0);
						var fileHash = new byte[MD5Size];
						stream.Read(fileHash, 0, MD5Size);
						ValidateHash(md5.Hash, fileHash);
					}

					return midpoints;
				}
			} catch {
				Dispose();
				throw;
			} finally {
				if (Runtime.IsUnixOrMac) {
					if (workItem != null)
						ReturnWorkItem(workItem);
				} else {
					if (stream != null)
						stream.Dispose();
				}
			}
		}


		private readonly byte[] TmpReadBuf = new byte[DefaultBufferSize];

		private void ReadUntilWithMd5(long nextPos, Stream fileStream, MD5 md5) {
			long toRead = nextPos - fileStream.Position;
			if (toRead < 0) throw new Exception("should not do negative reads.");
			while (toRead > 0) {
				var localReadCount = Math.Min(toRead, TmpReadBuf.Length);
				int read = fileStream.Read(TmpReadBuf, 0, (int)localReadCount);
				md5.TransformBlock(TmpReadBuf, 0, read, null, 0);
				toRead -= read;
			}
		}

		void ValidateHash(byte[] fromFile, byte[] computed) {
			if (computed == null)
				throw new CorruptIndexException(new HashValidationException("Calculated MD5 hash is null!"));
			if (fromFile == null)
				throw new CorruptIndexException(new HashValidationException("Read from file MD5 hash is null!"));

			if (computed.Length != fromFile.Length)
				throw new CorruptIndexException(
					new HashValidationException(
						string.Format(
							"Hash sizes differ! FileHash({0}): {1}, hash({2}): {3}.",
							computed.Length,
							BitConverter.ToString(computed),
							fromFile.Length,
							BitConverter.ToString(fromFile))));

			for (int i = 0; i < fromFile.Length; i++) {
				if (fromFile[i] != computed[i])
					throw new CorruptIndexException(
						new HashValidationException(
							string.Format(
								"Hashes are different! computed: {0}, hash: {1}.",
								BitConverter.ToString(computed),
								BitConverter.ToString(fromFile))));
			}
		}

		public IEnumerable<IndexEntry> IterateAllInOrder() {
			var workItem = GetWorkItem();
			try {
				workItem.Stream.Position = PTableHeader.Size;
				for (long i = 0, n = Count; i < n; i++) {
					yield return ReadNextNoSeek(workItem, _version);
				}
			} finally {
				ReturnWorkItem(workItem);
			}
		}

		public bool TryGetOneValue(ulong stream, long number, out long position) {
			IndexEntry entry;
			var hash = GetHash(stream);
			if (TryGetLargestEntry(hash, number, number, out entry)) {
				position = entry.Position;
				return true;
			}

			position = -1;
			return false;
		}

		public bool TryGetLatestEntry(ulong stream, out IndexEntry entry) {
			ulong hash = GetHash(stream);
			return TryGetLargestEntry(hash, 0, long.MaxValue, out entry);
		}

		private bool TryGetLargestEntry(ulong stream, long startNumber, long endNumber, out IndexEntry entry) {
			Ensure.Nonnegative(startNumber, "startNumber");
			Ensure.Nonnegative(endNumber, "endNumber");

			entry = TableIndex.InvalidIndexEntry;

			var startKey = BuildKey(stream, startNumber);
			var endKey = BuildKey(stream, endNumber);

			if (startKey.GreaterThan(_maxEntry) || endKey.SmallerThan(_minEntry))
				return false;

			var workItem = GetWorkItem();
			try {
				IndexEntryKey lowBoundsCheck, highBoundsCheck;
				var recordRange = LocateRecordRange(endKey, out lowBoundsCheck, out highBoundsCheck);

				long low = recordRange.Lower;
				long high = recordRange.Upper;
				while (low < high) {
					var mid = low + (high - low) / 2;
					IndexEntry midpoint = ReadEntry(_indexEntrySize, mid, workItem, _version);
					var midpointKey = new IndexEntryKey(midpoint.Stream, midpoint.Version);

					if (midpointKey.GreaterThan(lowBoundsCheck)) {
						throw new MaybeCorruptIndexException(String.Format(
							"Midpoint key (stream: {0}, version: {1}) > low bounds check key (stream: {2}, version: {3})",
							midpointKey.Stream, midpointKey.Version, lowBoundsCheck.Stream, lowBoundsCheck.Version));
					} else if (!midpointKey.GreaterEqualsThan(highBoundsCheck)) {
						throw new MaybeCorruptIndexException(String.Format(
							"Midpoint key (stream: {0}, version: {1}) < high bounds check key (stream: {2}, version: {3})",
							midpointKey.Stream, midpointKey.Version, highBoundsCheck.Stream, highBoundsCheck.Version));
					}

					if (midpointKey.GreaterThan(endKey)) {
						low = mid + 1;
						lowBoundsCheck = midpointKey;
					} else {
						high = mid;
						highBoundsCheck = midpointKey;
					}
				}

				var candEntry = ReadEntry(_indexEntrySize, high, workItem, _version);
				var candKey = new IndexEntryKey(candEntry.Stream, candEntry.Version);
				if (candKey.GreaterThan(endKey))
					throw new MaybeCorruptIndexException(string.Format(
						"candEntry ({0}@{1}) > startKey {2}, stream {3}, startNum {4}, endNum {5}, PTable: {6}.",
						candEntry.Stream, candEntry.Version, startKey, stream, startNumber, endNumber, Filename));
				if (candKey.SmallerThan(startKey))
					return false;
				entry = candEntry;
				return true;
			} finally {
				ReturnWorkItem(workItem);
			}
		}

		public bool TryGetOldestEntry(ulong stream, out IndexEntry entry) {
			ulong hash = GetHash(stream);
			return TryGetSmallestEntry(hash, 0, long.MaxValue, out entry);
		}

		private bool TryGetSmallestEntry(ulong stream, long startNumber, long endNumber, out IndexEntry entry) {
			Ensure.Nonnegative(startNumber, "startNumber");
			Ensure.Nonnegative(endNumber, "endNumber");

			entry = TableIndex.InvalidIndexEntry;

			var startKey = BuildKey(stream, startNumber);
			var endKey = BuildKey(stream, endNumber);

			if (startKey.GreaterThan(_maxEntry) || endKey.SmallerThan(_minEntry))
				return false;

			var workItem = GetWorkItem();
			try {
				IndexEntryKey lowBoundsCheck, highBoundsCheck;
				var recordRange = LocateRecordRange(startKey, out lowBoundsCheck, out highBoundsCheck);

				long low = recordRange.Lower;
				long high = recordRange.Upper;
				while (low < high) {
					var mid = low + (high - low + 1) / 2;
					var midpoint = ReadEntry(_indexEntrySize, mid, workItem, _version);
					var midpointKey = new IndexEntryKey(midpoint.Stream, midpoint.Version);

					if (midpointKey.GreaterThan(lowBoundsCheck)) {
						throw new MaybeCorruptIndexException(String.Format(
							"Midpoint key (stream: {0}, version: {1}) > low bounds check key (stream: {2}, version: {3})",
							midpointKey.Stream, midpointKey.Version, lowBoundsCheck.Stream, lowBoundsCheck.Version));
					} else if (!midpointKey.GreaterEqualsThan(highBoundsCheck)) {
						throw new MaybeCorruptIndexException(String.Format(
							"Midpoint key (stream: {0}, version: {1}) < high bounds check key (stream: {2}, version: {3})",
							midpointKey.Stream, midpointKey.Version, highBoundsCheck.Stream, highBoundsCheck.Version));
					}

					if (midpointKey.SmallerThan(startKey)) {
						high = mid - 1;
						highBoundsCheck = midpointKey;
					} else {
						low = mid;
						lowBoundsCheck = midpointKey;
					}
				}

				var candEntry = ReadEntry(_indexEntrySize, high, workItem, _version);
				var candidateKey = new IndexEntryKey(candEntry.Stream, candEntry.Version);
				if (candidateKey.SmallerThan(startKey))
					throw new MaybeCorruptIndexException(string.Format(
						"candEntry ({0}@{1}) < startKey {2}, stream {3}, startNum {4}, endNum {5}, PTable: {6}.",
						candEntry.Stream, candEntry.Version, startKey, stream, startNumber, endNumber, Filename));
				if (candidateKey.GreaterThan(endKey))
					return false;
				entry = candEntry;
				return true;
			} finally {
				ReturnWorkItem(workItem);
			}
		}

		public IEnumerable<IndexEntry> GetRange(ulong stream, long startNumber, long endNumber, int? limit = null) {
			Ensure.Nonnegative(startNumber, "startNumber");
			Ensure.Nonnegative(endNumber, "endNumber");

			ulong hash = GetHash(stream);

			var result = new List<IndexEntry>();
			var startKey = BuildKey(hash, startNumber);
			var endKey = BuildKey(hash, endNumber);

			if (startKey.GreaterThan(_maxEntry) || endKey.SmallerThan(_minEntry))
				return result;

			var workItem = GetWorkItem();
			try {
				IndexEntryKey lowBoundsCheck, highBoundsCheck;
				var recordRange = LocateRecordRange(endKey, out lowBoundsCheck, out highBoundsCheck);
				long low = recordRange.Lower;
				long high = recordRange.Upper;
				while (low < high) {
					var mid = low + (high - low) / 2;
					IndexEntry midpoint = ReadEntry(_indexEntrySize, mid, workItem, _version);
					var midpointKey = new IndexEntryKey(midpoint.Stream, midpoint.Version);

					if (midpointKey.GreaterThan(lowBoundsCheck)) {
						throw new MaybeCorruptIndexException(String.Format(
							"Midpoint key (stream: {0}, version: {1}) > low bounds check key (stream: {2}, version: {3})",
							midpointKey.Stream, midpointKey.Version, lowBoundsCheck.Stream, lowBoundsCheck.Version));
					} else if (!midpointKey.GreaterEqualsThan(highBoundsCheck)) {
						throw new MaybeCorruptIndexException(String.Format(
							"Midpoint key (stream: {0}, version: {1}) < high bounds check key (stream: {2}, version: {3})",
							midpointKey.Stream, midpointKey.Version, highBoundsCheck.Stream, highBoundsCheck.Version));
					}

					if (midpointKey.SmallerEqualsThan(endKey)) {
						high = mid;
						highBoundsCheck = midpointKey;
					} else {
						low = mid + 1;
						lowBoundsCheck = midpointKey;
					}
				}

				PositionAtEntry(_indexEntrySize, high, workItem);
				for (long i = high, n = Count; i < n; ++i) {
					IndexEntry entry = ReadNextNoSeek(workItem, _version);
					var candidateKey = new IndexEntryKey(entry.Stream, entry.Version);
					if (candidateKey.GreaterThan(endKey))
						throw new MaybeCorruptIndexException(string.Format(
							"entry ({0}@{1}) > endKey {2}, stream {3}, startNum {4}, endNum {5}, PTable: {6}.",
							entry.Stream, entry.Version, startKey, stream, startNumber, endNumber, Filename));
					if (candidateKey.SmallerThan(startKey))
						return result;
					result.Add(entry);
					if (result.Count == limit) break;
				}

				return result;
			} finally {
				ReturnWorkItem(workItem);
			}
		}

		private ulong GetHash(ulong hash) {
			return _version == PTableVersions.IndexV1 ? hash >> 32 : hash;
		}

		private static IndexEntryKey BuildKey(ulong stream, long version) {
			return new IndexEntryKey(stream, version);
		}

		private Range LocateRecordRange(IndexEntryKey key, out IndexEntryKey lowKey, out IndexEntryKey highKey) {
			lowKey = new IndexEntryKey(ulong.MaxValue, long.MaxValue);
			highKey = new IndexEntryKey(ulong.MinValue, long.MinValue);

			var midpoints = _midpoints;
			if (midpoints == null)
				return new Range(0, Count - 1);
			long lowerMidpoint = LowerMidpointBound(midpoints, key);
			long upperMidpoint = UpperMidpointBound(midpoints, key);

			lowKey = midpoints[lowerMidpoint].Key;
			highKey = midpoints[upperMidpoint].Key;

			return new Range(midpoints[lowerMidpoint].ItemIndex, midpoints[upperMidpoint].ItemIndex);
		}

		private long LowerMidpointBound(Midpoint[] midpoints, IndexEntryKey key) {
			long l = 0;
			long r = midpoints.Length - 1;
			while (l < r) {
				long m = l + (r - l + 1) / 2;
				if (midpoints[m].Key.GreaterThan(key))
					l = m;
				else
					r = m - 1;
			}

			return l;
		}

		private long UpperMidpointBound(Midpoint[] midpoints, IndexEntryKey key) {
			long l = 0;
			long r = midpoints.Length - 1;
			while (l < r) {
				long m = l + (r - l) / 2;
				if (midpoints[m].Key.SmallerThan(key))
					r = m;
				else
					l = m + 1;
			}

			return r;
		}

		private static void PositionAtEntry(int indexEntrySize, long indexNum, WorkItem workItem) {
			workItem.Stream.Seek(indexEntrySize * indexNum + PTableHeader.Size, SeekOrigin.Begin);
		}

		private static IndexEntry ReadEntry(int indexEntrySize, long indexNum, WorkItem workItem, int ptableVersion) {
			long seekTo = indexEntrySize * indexNum + PTableHeader.Size;
			workItem.Stream.Seek(seekTo, SeekOrigin.Begin);
			return ReadNextNoSeek(workItem, ptableVersion);
		}

		private static IndexEntry ReadNextNoSeek(WorkItem workItem, int ptableVersion) {
			long version = (ptableVersion >= PTableVersions.IndexV3)
				? workItem.Reader.ReadInt64()
				: workItem.Reader.ReadInt32();
			ulong stream = ptableVersion == PTableVersions.IndexV1
				? workItem.Reader.ReadUInt32()
				: workItem.Reader.ReadUInt64();
			long position = workItem.Reader.ReadInt64();
			return new IndexEntry(stream, version, position);
		}

		private WorkItem GetWorkItem() {
			try {
				return _workItems.Get();
			} catch (ObjectPoolDisposingException) {
				throw new FileBeingDeletedException();
			} catch (ObjectPoolMaxLimitReachedException) {
				throw new Exception("Unable to acquire work item.");
			}
		}

		private void ReturnWorkItem(WorkItem workItem) {
			_workItems.Return(workItem);
		}

		public void MarkForDestruction() {
			_deleteFile = true;
			_workItems.MarkForDisposal();
		}

		public void Dispose() {
			_deleteFile = false;
			_workItems.MarkForDisposal();
		}

		private void OnAllWorkItemsDisposed() {
			File.SetAttributes(_filename, FileAttributes.Normal);
			if (_deleteFile)
				File.Delete(_filename);
			_destroyEvent.Set();
		}

		public void WaitForDisposal(int timeout) {
			if (!_destroyEvent.Wait(timeout))
				throw new TimeoutException();
		}

		public void WaitForDisposal(TimeSpan timeout) {
			if (!_destroyEvent.Wait(timeout))
				throw new TimeoutException();
		}

		internal struct Midpoint {
			public readonly IndexEntryKey Key;
			public readonly long ItemIndex;

			public Midpoint(IndexEntryKey key, long itemIndex) {
				Key = key;
				ItemIndex = itemIndex;
			}
		}

		internal struct IndexEntryKey {
			public ulong Stream;
			public long Version;

			public IndexEntryKey(ulong stream, long version) {
				Stream = stream;
				Version = version;
			}

			public bool GreaterThan(IndexEntryKey other) {
				if (Stream == other.Stream) {
					return Version > other.Version;
				}

				return Stream > other.Stream;
			}

			public bool SmallerThan(IndexEntryKey other) {
				if (Stream == other.Stream) {
					return Version < other.Version;
				}

				return Stream < other.Stream;
			}

			public bool GreaterEqualsThan(IndexEntryKey other) {
				if (Stream == other.Stream) {
					return Version >= other.Version;
				}

				return Stream >= other.Stream;
			}

			public bool SmallerEqualsThan(IndexEntryKey other) {
				if (Stream == other.Stream) {
					return Version <= other.Version;
				}

				return Stream <= other.Stream;
			}

			public override string ToString() {
				return string.Format("Stream: {0}, Version: {1}", Stream, Version);
			}
		}

		private class WorkItem : IDisposable {
			public readonly FileStream Stream;
			public readonly BinaryReader Reader;

			public WorkItem(string filename, int bufferSize) {
				Stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize,
					FileOptions.RandomAccess);
				Reader = new BinaryReader(Stream);
			}

			~WorkItem() {
				Dispose(false);
			}

			public void Dispose() {
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing) {
				if (disposing) {
					Stream.Dispose();
					Reader.Dispose();
				}
			}
		}
	}
}
