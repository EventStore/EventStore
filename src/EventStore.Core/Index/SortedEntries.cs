using System;
using System.Buffers;
using System.Collections.Generic;
using DotNext.Buffers;
using EventStore.Common.Utils;

namespace EventStore.Core.Index;

public readonly struct MemEntry {
	public int Index { get; }
	public long Revision { get; }
	public long Position { get; }

	public static MemEntry Default => new(-1, -1, -1);

	public MemEntry(int index, long revision, long position) {
		Index = index;
		Revision = revision;
		Position = position;
	}

	public int CompareTo(long revision, long position) {
		return CompareTo(new MemEntry(-1, revision, position));
	}

	public int CompareTo(MemEntry other) {
		if (Revision < other.Revision)
			return -1;

		if (Revision > other.Revision)
			return 1;

		if (Position < other.Position)
			return -1;

		return Position > other.Position ? 1 : 0;
	}
}

public struct SortedEntries {
	private const int MemTableEntrySize = 16;
	private readonly PooledArrayBufferWriter<byte> _block;
	private MemEntry _lastEntry = MemEntry.Default;
	private int _count = 0;

	public SortedEntries() {
		_block = new PooledArrayBufferWriter<byte> {
			BufferPool = ArrayPool<byte>.Shared,
		};
	}

	public void Add(long revision, long position) {
		var newEntry = new MemEntry(-1, revision, position);
		switch (newEntry.CompareTo(_lastEntry)) {
			case 0:
				// The last entry has already the same value, we don't have to do anything.
				break;
			case 1:
				// The new entry is greater than our last one, we just need to append.
				var span = _block.GetSpan(MemTableEntrySize);
				WriteEntry(span[..MemTableEntrySize], revision, position);
				_block.Advance(MemTableEntrySize);
				_lastEntry = newEntry;
				_count++;
				break;
			case -1:
				// The new entry is smaller than our last one, we need to insert the new entry at the right location
				// to keep the table sorted.
				var entry = ClosestGreaterOrEqualEntry(revision, position);

				// It doesn't allocate if the block has enough capacity. However it will shift the block to the right at
				// the insertion point (array copy).
				var buf = ArrayPool<byte>.Shared.Rent(MemTableEntrySize);
				var tmp = buf.AsSpan();
				WriteEntry(tmp[..MemTableEntrySize], revision, position);
				_block.Insert(entry.Index * MemTableEntrySize, tmp[..MemTableEntrySize]);
				ArrayPool<byte>.Shared.Return(buf);

				_count++;
					break;
		}
	}

	private static void WriteEntry(Span<byte> span, long revision, long position) {
		BitConverter.TryWriteBytes(span[..8], (ulong)revision);
		BitConverter.TryWriteBytes(span[8..], (ulong)position);
	}

	private static MemEntry ReadEntryAt(ArraySegment<byte> seg, int index) {
		var line = seg.Slice(index * MemTableEntrySize, MemTableEntrySize);
		return new MemEntry(index, (long)BitConverter.ToUInt64(line[..8]), (long)BitConverter.ToUInt64(line[8..]));
	}

	public bool TryGetPosition(long revision, out long position) {
		var buffer = _block.WrittenArray;
		var low = 0;
		var high = _count - 1;

		position = 0;

		while (low <= high) {
			var mid = (low + high) / 2;
			var current = ReadEntryAt(buffer, mid);
			switch (current.Revision.CompareTo(revision)) {
				case -1:
					low = mid + 1;
					break;
				case 1:
					high = mid - 1;
					break;
				case 0:
					// We found the correct stream revision
					position = current.Position;

					// We take care of existing duplicates on the edge.
					for (var i = mid + 1; i < _count; i++) {
						current = ReadEntryAt(buffer, i);
						if (current.Revision != revision)
							break;

						position = current.Position;
					}

					return true;
			}
		}

		return false;
	}
	
	public MemEntry First() {
		return ReadEntryAt(_block.WrittenArray, 0);
	}
	
	public MemEntry Last() {
		return ReadEntryAt(_block.WrittenArray, _count - 1);
	}

	public IEnumerable<MemEntry> List() {
		var buffer = _block.WrittenArray;

		for (var i = 0; i < _count; i++) {
			yield return ReadEntryAt(buffer, i);
		}
	}

	public IEnumerable<MemEntry> ListFromEnd() {
		return ListFromEnd(_count - 1);
	}
	
	public IEnumerable<MemEntry> ListFromEnd(int start) {
		Ensure.Nonnegative(_count - 1 - start, "Starting point is greater than the length of the table");
		var buffer = _block.WrittenArray;

		for (var i = start; i >= 0; --i) {
			yield return ReadEntryAt(buffer, i);
		}
	}	
	
	public MemEntry ClosestGreaterOrEqualEntry(long revision, long position) {
		var buffer = _block.WrittenArray;
		var entry = new MemEntry(-1, revision, position);
		var low = 0;
		var high = _count - 1;
		var closest = MemEntry.Default;
			
		while (low <= high) {
			var mid = (low + high) / 2;
			var current = ReadEntryAt(buffer, mid);
			
			switch (current.CompareTo(entry)) {
				case -1:
					low = mid + 1;
					break;
				case 1:
					closest = current;
					high = mid - 1;
					break;
				case 0:
					// We found the correct stream revision
					closest = current;
					high = -1;
					break;
			}
		}

		return closest;
	}
	
	public MemEntry ClosestLowerOrEqualEntry(long revision, long position) {
		var buffer = _block.WrittenArray;
		var entry = new MemEntry(-1, revision, position);
		var low = 0;
		var high = _count - 1;
		var closest = MemEntry.Default;
			
		while (low <= high) {
			var mid = (low + high) / 2;
			var current = ReadEntryAt(buffer, mid);
			
			switch (current.CompareTo(entry)) {
				case -1:
					closest = current;
					low = mid + 1;
					break;
				case 1:
					high = mid - 1;
					break;
				case 0:
					// We found the correct stream revision
					closest = current;
					high = -1;
					break;
			}
		}

		return closest;
	}

	public bool PositionUpperBound(long position, out MemEntry result, Func<MemEntry, bool> keepGoing) {
		MemEntry entry = MemEntry.Default;
		result = entry;
		
		if (_count == 0)
			return false;

		var buffer = _block.WrittenArray;
		entry = ReadEntryAt(buffer, _count - 1);

		if (!keepGoing(entry) || entry.Position.CompareTo(position) < 0)
			return false;
		
		var low = 0;
		var high = _count - 1;

		while (low < high) {
			var mid = low + (high - low + 1) / 2;
			entry = ReadEntryAt(buffer, mid);

			if (!keepGoing(entry))
				break;

			if (entry.Position.CompareTo(position) <= 0)
				low = mid;
			else
				high = mid - 1;
		}

		entry = ReadEntryAt(buffer, low);

		if (!keepGoing(entry))
			return false;
		
		result = entry;
		return true;
	} 
}
