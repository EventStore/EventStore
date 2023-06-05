using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;

namespace EventStore.Core.Index {
	public class HashListMemTable : IMemTable, ISearchTable {
		private static readonly IComparer<Entry> MemTableComparer = new EventNumberComparer();
		private static readonly IComparer<Entry> LogPosComparer = new LogPositionComparer();

		public long Count {
			get { return _count; }
		}

		public Guid Id {
			get { return _id; }
		}

		public byte Version {
			get { return _version; }
		}

		private readonly Dictionary<ulong, PooledBufferWriter<byte>> _hash;
		private readonly Guid _id = Guid.NewGuid();
		private readonly byte _version;
		private int _count;

		private int _isConverting;

		public HashListMemTable(byte version, int maxSize) {
			_version = version;
			_hash = new Dictionary<ulong, PooledBufferWriter<byte>>();
		}

		public bool MarkForConversion() {
			return Interlocked.CompareExchange(ref _isConverting, 1, 0) == 0;
		}

		public void Add(ulong stream, long version, long position) {
			AddEntries(new[] {new IndexEntry(stream, version, position)});
		}

		public void AddEntries(IList<IndexEntry> entries) {
			Ensure.NotNull(entries, "entries");
			Ensure.Positive(entries.Count, "entries.Count");

			// only one thread at a time can write
			Interlocked.Add(ref _count, entries.Count);
			
			var stream = GetHash(entries[0].Stream);
			lock(_hash) {
				if (!_hash.TryGetValue(stream, out var block)) {
					block = new PooledBufferWriter<byte> {
						BufferAllocator = ArrayPool<byte>.Shared.ToAllocator(),
					};
				}
				
				foreach (var entry in entries) {
					if (GetHash(entry.Stream) != stream)
						throw new Exception("Not all index entries in a bulk have the same stream hash.");
							
					Ensure.Nonnegative(entry.Version, "entry.Version");
					Ensure.Nonnegative(entry.Position, "entry.Position");

					block.WriteUInt64(stream, true);
					block.WriteUInt64((ulong)entry.Version, true);
					block.WriteUInt64((ulong)entry.Position, true);
				}

				_hash[stream] = block;
			}
		}

		public bool TryGetOneValue(ulong stream, long number, out long position) {
			Ensure.Nonnegative(number, "number");
			ulong hash = GetHash(stream);

			position = 0;

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return false;
				
				var entryCount = block.WrittenCount / 24;
				var buffer = block.WrittenMemory.AsStream();

				return TryGetPosition(buffer, (ulong)number, entryCount, out position);
			}
		}

		public bool TryGetLatestEntry(ulong stream, out IndexEntry entry) {
			ulong hash = GetHash(stream);
			entry = TableIndex.InvalidIndexEntry;

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return false;

				var count = block.WrittenCount / 24;
				var last = count - 1;
				var buffer = block.WrittenMemory.AsStream();

				buffer.Seek((last * 24) + 8, SeekOrigin.Begin);
				
				entry = new IndexEntry(hash, (long) buffer.Read<ulong>(), (long) buffer.Read<ulong>());
				return true;
			}
		}

		public bool TryGetLatestEntry(ulong stream, long beforePosition, Func<IndexEntry, bool> isForThisStream, out IndexEntry entry) {
			Ensure.Nonnegative(beforePosition, nameof(beforePosition));

			ulong hash = GetHash(stream);
			entry = TableIndex.InvalidIndexEntry;

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return false;

				var count = block.WrittenCount / 24;
				var last = count - 1;
				var buffer = block.WrittenMemory.AsStream();
				IndexEntry prev = TableIndex.InvalidIndexEntry;

				var found = false;
				for (var i = 0; i < count; i++) {
					buffer.Seek(8, SeekOrigin.Current);
					var temp = new IndexEntry(hash, (long) buffer.Read<ulong>(), (long) buffer.Read<ulong>());
					if (!isForThisStream(entry))
						break;

					prev = temp;
					found = true;
				}

				if (!found)
					return false;

				entry = prev;
				return true;
			}
		}

		public bool TryGetOldestEntry(ulong stream, out IndexEntry entry) {
			ulong hash = GetHash(stream);
			entry = TableIndex.InvalidIndexEntry;

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return false;

				var buffer = block.WrittenMemory.AsStream();
				buffer.Seek(8, SeekOrigin.Begin);
				entry = new IndexEntry(hash, (long) buffer.Read<ulong>(), (long) buffer.Read<ulong>());

				return false;
			}
		}

		public bool TryGetNextEntry(ulong stream, long afterNumber, out IndexEntry entry) {
			Ensure.Nonnegative(afterNumber, nameof(afterNumber));

			ulong hash = GetHash(stream);
			entry = TableIndex.InvalidIndexEntry;

			if (afterNumber >= long.MaxValue)
				return false;

			SortedList<Entry, byte> list;
			if (_hash.TryGetValue(hash, out list)) {
				if (!Monitor.TryEnter(list, 10000))
					throw new UnableToAcquireLockInReasonableTimeException();
				try {
					int endIdx = list.LowerBound(new Entry(afterNumber + 1, 0));
					if (endIdx == -1)
						return false;

					var e = list.Keys[endIdx];
					entry = new IndexEntry(hash, e.EvNum, e.LogPos);
					return true;
				} finally {
					Monitor.Exit(list);
				}
			}

			return false;
		}

		public bool TryGetPreviousEntry(ulong stream, long beforeNumber, out IndexEntry entry) {
			if (beforeNumber < 0)
				throw new ArgumentOutOfRangeException(nameof(beforeNumber));

			ulong hash = GetHash(stream);
			entry = TableIndex.InvalidIndexEntry;

			if (beforeNumber <= 0)
				return false;

			SortedList<Entry, byte> list;
			if (_hash.TryGetValue(hash, out list)) {
				if (!Monitor.TryEnter(list, 10000))
					throw new UnableToAcquireLockInReasonableTimeException();
				try {
					int endIdx = list.UpperBound(new Entry(beforeNumber - 1, long.MaxValue));
					if (endIdx == -1)
						return false;

					var e = list.Keys[endIdx];
					entry = new IndexEntry(hash, e.EvNum, e.LogPos);
					return true;
				} finally {
					Monitor.Exit(list);
				}
			}

			return false;
		}

		public IEnumerable<IndexEntry> IterateAllInOrder() {
			//Log.Trace("Sorting array in HashListMemTable.IterateAllInOrder...");

			var keys = _hash.Keys.ToArray();
			Array.Sort(keys, new ReverseComparer<ulong>());

			foreach (var key in keys) {
				var list = _hash[key];
				for (int i = list.Count - 1; i >= 0; --i) {
					var x = list.Keys[i];
					yield return new IndexEntry(key, x.EvNum, x.LogPos);
				}
			}

			//Log.Trace("Sorting array in HashListMemTable.IterateAllInOrder... DONE!");
		}

		public void Clear() {
			_hash.Clear();
		}

		public IList<IndexEntry> GetRange(ulong stream, long startNumber, long endNumber, int? limit = null) {
			if (startNumber < 0)
				throw new ArgumentOutOfRangeException("startNumber");
			if (endNumber < 0)
				throw new ArgumentOutOfRangeException("endNumber");

			ulong hash = GetHash(stream);
			var ret = new List<IndexEntry>();

			SortedList<Entry, byte> list;
			if (_hash.TryGetValue(hash, out list)) {
				if (!Monitor.TryEnter(list, 10000)) throw new UnableToAcquireLockInReasonableTimeException();
				try {
					var endIdx = list.UpperBound(new Entry(endNumber, long.MaxValue));
					for (int i = endIdx; i >= 0; i--) {
						var key = list.Keys[i];
						if (key.EvNum < startNumber || ret.Count == limit)
							break;
						ret.Add(new IndexEntry(hash, version: key.EvNum, position: key.LogPos));
					}
				} finally {
					Monitor.Exit(list);
				}
			}

			return ret;
		}

		private ulong GetHash(ulong hash) {
			return _version == PTableVersions.IndexV1 ? hash >> 32 : hash;
		}
		
		private static bool TryGetPosition(Stream buffer, ulong expected, int count, out long position) {
			var low = 0;
			var high = count;

			position = 0;
			
			while (low <= high) {
				var mid = (low + high) / 2;
				// We move to the mid-th entry and also skip the first 8 bytes dedicated to the stream hash.
				buffer.Seek((mid * 24) + 8, SeekOrigin.Begin);
				var revision = buffer.Read<ulong>();
				switch (revision.CompareTo(expected)) {
					case -1:
						low = mid + 1;
						break;
					case 1:
						high = mid - 1;
						break;
					case 0:
						// We found the correct stream revision
						position = (long) buffer.Read<ulong>();
						return true;
				}
			}
			
			return false;
		}
		
		private struct Entry {
			public readonly long EvNum;
			public readonly long LogPos;

			public Entry(long evNum, long logPos) {
				EvNum = evNum;
				LogPos = logPos;
			}
		}

		private class EventNumberComparer : IComparer<Entry> {
			public int Compare(Entry x, Entry y) {
				if (x.EvNum < y.EvNum) return -1;
				if (x.EvNum > y.EvNum) return 1;
				if (x.LogPos < y.LogPos) return -1;
				if (x.LogPos > y.LogPos) return 1;
				return 0;
			}
		}

		private class LogPositionComparer : IComparer<Entry> {
			public int Compare(Entry x, Entry y) {
				if (x.LogPos < y.LogPos) return -1;
				if (x.LogPos > y.LogPos) return 1;
				return 0;
			}
		}
	}

	public class ReverseComparer<T> : IComparer<T> where T : IComparable {
		public int Compare(T x, T y) {
			return -x.CompareTo(y);
		}
	}
}
