using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Common.Utils;

namespace EventStore.Core.Index {
	public class HashListMemTable : IMemTable, ISearchTable {
		private const int MemTableEntrySize = 16;
		
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
				
				var entryCount = block.WrittenCount / MemTableEntrySize;
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

				var count = block.WrittenCount / MemTableEntrySize;
				var last = count - 1;
				var buffer = block.WrittenMemory.AsStream();

				buffer.Seek(last * MemTableEntrySize, SeekOrigin.Begin);
				
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

				var entryCount = block.WrittenCount / MemTableEntrySize;
				var buffer = block.WrittenMemory.AsStream();
				IndexEntry prev = TableIndex.InvalidIndexEntry;

				var found = false;
				for (var i = 0; i < entryCount; i++) {
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

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return false;

				var count = block.WrittenCount / MemTableEntrySize;
				var buffer = block.WrittenMemory.AsStream();

				if (!ClosestGreaterOrEqualRevision(buffer, (ulong)(afterNumber + 1), count, out var index))
					return false;

				buffer.Seek((index * 24) + 8, SeekOrigin.Begin);
				entry = new IndexEntry(hash, (long) buffer.Read<ulong>(), (long) buffer.Read<ulong>());
				return true;
			}
		}

		public bool TryGetPreviousEntry(ulong stream, long beforeNumber, out IndexEntry entry) {
			Ensure.Nonnegative(beforeNumber, nameof(beforeNumber));

			ulong hash = GetHash(stream);
			entry = TableIndex.InvalidIndexEntry;

			if (beforeNumber == 0) 
				return false;

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return false;

				var count = block.WrittenCount / MemTableEntrySize;
				var buffer = block.WrittenMemory.AsStream();

				if (!ClosestLowerOrEqualRevision(buffer, (ulong)(beforeNumber + 1), count, out var index))
					return false;

				buffer.Seek(index * MemTableEntrySize, SeekOrigin.Begin);
				entry = new IndexEntry(hash, (long) buffer.Read<ulong>(), (long) buffer.Read<ulong>());
				return true;
			}
		}

		public IEnumerable<IndexEntry> IterateAllInOrder() {
			lock (_hash) {
				var keys = _hash.Keys.ToArray();
				Array.Sort(keys, new ReverseComparer<ulong>());

				foreach (var key in keys) {
					var block = _hash[key];
					var count = block.WrittenCount / MemTableEntrySize;
					var buffer = block.WrittenMemory.AsStream();

					for (int i = count - 1; i >= 0; --i) {
						buffer.Seek(i * MemTableEntrySize, SeekOrigin.Begin);
						yield return new IndexEntry(key, (long)buffer.Read<ulong>(), (long)buffer.Read<ulong>());
					}
				}
			}
		}

		public void Clear() {
			lock (_hash) {
				_hash.Clear();
			}
		}

		public IList<IndexEntry> GetRange(ulong stream, long startNumber, long endNumber, int? limit = null) {
			Ensure.Nonnegative(startNumber, nameof(startNumber));
			Ensure.Nonnegative(endNumber, nameof(endNumber));

			ulong hash = GetHash(stream);
			var ret = new List<IndexEntry>();

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return ret;

				var count = block.WrittenCount / MemTableEntrySize;
				var buffer = block.WrittenMemory.AsStream();
				if (!ClosestLowerOrEqualRevision(buffer, (ulong) endNumber, count, out var endIdx))
					return ret;

				for (int i = endIdx; i >= 0; i--) {
					buffer.Seek(i * MemTableEntrySize, SeekOrigin.Begin);
					var entry = new IndexEntry(hash, (long)buffer.Read<ulong>(), (long)buffer.Read<ulong>());
					
					if (entry.Version < startNumber || ret.Count == limit)
						break;
					
					ret.Add(entry);
				}

				return ret;
			}
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
				buffer.Seek(mid * MemTableEntrySize, SeekOrigin.Begin);
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
						position = (long)buffer.Read<ulong>();
						
						// We take care of existing duplicates on the edge.
						for (var i = mid + 1; i < count; i++) {
							if (buffer.Read<ulong>() != expected)
								break;

							position = (long) buffer.Read<ulong>();
						}
						
						return true;
				}
			}
			
			return false;
		}
		
		private static bool ClosestGreaterOrEqualRevision(Stream buffer, ulong expected, int count, out int index) {
			var low = 0;
			var high = count;
			var closest = -1;

			index = -1;
			
			while (low <= high) {
				var mid = (low + high) / 2;
				// We move to the mid-th entry and also skip the first 8 bytes dedicated to the stream hash.
				buffer.Seek(mid * MemTableEntrySize, SeekOrigin.Begin);
				var revision = buffer.Read<ulong>();
				switch (revision.CompareTo(expected)) {
					case -1:
						low = mid + 1;
						break;
					case 1:
						closest = mid;
						high = mid - 1;
						break;
					case 0:
						// We found the correct stream revision
						index = mid;
						return true;
				}
			}

			if (closest == -1)
				return false;
			
			index = closest;
			return true;
		}
		
		private static bool ClosestLowerOrEqualRevision(Stream buffer, ulong expected, int count, out int index) {
			var low = 0;
			var high = count;
			var closest = -1;

			index = -1;
			
			while (low <= high) {
				var mid = (low + high) / 2;
				buffer.Seek(mid * MemTableEntrySize, SeekOrigin.Begin);
				var revision = buffer.Read<ulong>();
				switch (revision.CompareTo(expected)) {
					case -1:
						closest = mid;
						low = mid + 1;
						break;
					case 1:
						high = mid - 1;
						break;
					case 0:
						// We found the correct stream revision so we exit the loop
						closest = mid;
						high = -1;
						
						// We ignore the encoded position and place ourselves onto
						// the next index entry.
						buffer.Seek(8, SeekOrigin.Current);
						break;
				}
			}

			if (closest == -1)
				return false;

			index = closest;
			
			// We take care of existing duplicates on the edge.
			for (var i = closest + 1; i < count; i++) {
				if (buffer.Read<ulong>() != expected)
					break;

				index = i;
				// We ignore the encoded position because it's not needed.
				buffer.Seek(8, SeekOrigin.Current);
			}

			return true;
		}
	}

	public class ReverseComparer<T> : IComparer<T> where T : IComparable {
		public int Compare(T x, T y) {
			return -x.CompareTo(y);
		}
	}
}
