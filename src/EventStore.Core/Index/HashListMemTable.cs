using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.Core.Index {
	public class HashListMemTable : IMemTable, ISearchTable{
		public long Count {
			get { return _count; }
		}

		public Guid Id {
			get { return _id; }
		}

		public byte Version {
			get { return _version; }
		}

		private readonly Dictionary<ulong, SortedEntries> _hash;
		private readonly Guid _id = Guid.NewGuid();
		private readonly byte _version;
		private int _count;

		private int _isConverting;

		public HashListMemTable(byte version, int maxSize) {
			_version = version;
			_hash = new Dictionary<ulong, SortedEntries>();
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
					block = new SortedEntries();
				}
				
				foreach (var entry in entries) {
					if (GetHash(entry.Stream) != stream)
						throw new Exception("Not all index entries in a bulk have the same stream hash.");
							
					Ensure.Nonnegative(entry.Version, "entry.Version");
					Ensure.Nonnegative(entry.Position, "entry.Position");

					block.Add(entry.Version, entry.Position);
				}

				_hash[stream] = block;
			}
		}

		public bool TryGetOneValue(ulong stream, long number, out long position) {
			Ensure.Nonnegative(number, "number");
			ulong hash = GetHash(stream);

			position = 0;

			lock (_hash) {
				return _hash.TryGetValue(hash, out var block)
				       && block.TryGetPosition(number, out position);
			}
		}

		public bool TryGetLatestEntry(ulong stream, out IndexEntry entry) {
			ulong hash = GetHash(stream);
			entry = TableIndex.InvalidIndexEntry;

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return false;

				var mem = block.Last();
				entry = new IndexEntry(hash, mem.Revision, mem.Position);
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

				if (!block.PositionUpperBound(beforePosition - 1, out var mem, m => isForThisStream(new IndexEntry(hash, m.Revision, m.Position))))
					return false;
				
				entry = new IndexEntry(hash, mem.Revision, mem.Position);
				return true;
			}
		}

		public bool TryGetOldestEntry(ulong stream, out IndexEntry entry) {
			ulong hash = GetHash(stream);
			entry = TableIndex.InvalidIndexEntry;

			lock (_hash) {
				if (!_hash.TryGetValue(hash, out var block))
					return false;

				var mem = block.First();
				entry = new IndexEntry(hash, mem.Revision, mem.Position);

				return true;
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

				var mem = block.ClosestGreaterOrEqualEntry(afterNumber + 1, 0);
				if (mem.Index == -1)
					return false;
				
				entry = new IndexEntry(hash, mem.Revision, mem.Position);
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

				var mem = block.ClosestLowerOrEqualEntry(beforeNumber - 1, 0);
				if (mem.Index == -1)
					return false;
				
				entry = new IndexEntry(hash, mem.Revision, mem.Position);
				return true;
			}
		}

		public IEnumerable<IndexEntry> IterateAllInOrder() {
			lock (_hash) {
				var keys = _hash.Keys.ToArray();
				Array.Sort(keys, new ReverseComparer<ulong>());

				foreach (var key in keys) {
					var block = _hash[key];

					foreach (var mem in block.ListFromEnd()) {
						yield return new IndexEntry(key, mem.Revision, mem.Position);
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

				var mem = block.ClosestLowerOrEqualEntry(endNumber, long.MaxValue);
				if (mem.Index == -1)
					return ret;

				foreach (var elem in block.ListFromEnd(mem.Index)) {
					var entry = new IndexEntry(hash, elem.Revision, elem.Position);
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
	}

	public class ReverseComparer<T> : IComparer<T> where T : IComparable {
		public int Compare(T x, T y) {
			return -x.CompareTo(y);
		}
	}
}
