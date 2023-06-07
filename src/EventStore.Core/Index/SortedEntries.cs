using System.Buffers;
using System.Collections.Generic;
using System.IO;
using DotNext.Buffers;
using DotNext.IO;
using EventStore.Common.Utils;

namespace EventStore.Core.Index;

public readonly struct MemEntry {
	public int Index { get; }
	public long Revision { get; }
	public long Position { get; }

	public static MemEntry Default => new(-1, 0, 0);

	public MemEntry(int index, long revision, long position) {
		Index = index;
		Revision = revision;
		Position = position;
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

	public bool SameRevisionAndPosition(long revision, long position) {
		var entry = new MemEntry(-1, revision, position);
		return CompareTo(entry) == 0;
	}
}

public struct SortedEntries {
	private const int MemTableEntrySize = 16;
	private readonly PooledBufferWriter<byte> _block;
	private long _lastRevision = -1;

	private int Count => _block.WrittenCount / MemTableEntrySize;

	public SortedEntries() {
		_block = new PooledBufferWriter<byte> {
			BufferAllocator = ArrayPool<byte>.Shared.ToAllocator(),
		};
	}

	public void Add(long revision, long position) {
		if (_lastRevision > revision) {
			var entry = ClosestGreaterOrEqualEntry(revision, position);
			
			if (entry.SameRevisionAndPosition(revision, position))
				return;

			byte[] afterBuffer;
			byte[] beforeBuffer = null;
			
			// If we have to write the entry at the beginning of the table.
			if (entry.Index <= 0) {
				afterBuffer = ArrayPool<byte>.Shared.Rent(_block.WrittenCount);
				_block.WrittenMemory.CopyTo(afterBuffer);
				_block.Clear(true);
				_block.WriteUInt64((ulong)revision, true);
				_block.WriteUInt64((ulong)position, true);
				_block.Write(afterBuffer);
			} else {
				beforeBuffer = ArrayPool<byte>.Shared.Rent(entry.Index * MemTableEntrySize);
				afterBuffer = ArrayPool<byte>.Shared.Rent((Count - entry.Index) * MemTableEntrySize);
				
				_block.WrittenMemory.Slice(0, entry.Index * MemTableEntrySize).CopyTo(beforeBuffer);
				_block.WrittenMemory.Slice((Count - entry.Index - 1) * MemTableEntrySize).CopyTo(afterBuffer);
				_block.Clear(true);
				_block.Write(beforeBuffer);
				_block.WriteUInt64((ulong)revision, true);
				_block.WriteUInt64((ulong)position, true);
				_block.Write(afterBuffer);
			}

			if (beforeBuffer != null)
				ArrayPool<byte>.Shared.Return(beforeBuffer);
			
			ArrayPool<byte>.Shared.Return(afterBuffer);
		} else {
			_block.WriteUInt64((ulong)revision, true);
			_block.WriteUInt64((ulong)position, true);
			_lastRevision = revision;
		}
	}

	public bool TryGetPosition(long revision, out long position) {
		using var buffer = _block.WrittenMemory.AsStream();
		var low = 0;
		var high = Count - 1;

		position = 0;

		while (low <= high) {
			var mid = (low + high) / 2;
			buffer.Seek(mid * MemTableEntrySize, SeekOrigin.Begin);
			var current = buffer.Read<ulong>();
			switch (current.CompareTo(revision)) {
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
					for (var i = mid + 1; i < Count; i++) {
						if (buffer.Read<ulong>() != (ulong)revision)
							break;

						position = (long)buffer.Read<ulong>();
					}

					return true;
			}
		}

		return false;
	}
	
	public MemEntry First() {
		using var buffer = _block.WrittenMemory.AsStream();
		buffer.Seek(0, SeekOrigin.Begin);

		return new MemEntry(0, (long)buffer.Read<ulong>(), (long)buffer.Read<ulong>());
	}
	
	public MemEntry Last() {
		using var buffer = _block.WrittenMemory.AsStream();
		var lastIdx = Count - 1;
		buffer.Seek(lastIdx * MemTableEntrySize, SeekOrigin.Begin);

		return new MemEntry(lastIdx, (long)buffer.Read<ulong>(), (long)buffer.Read<ulong>());
	}

	public IEnumerable<MemEntry> List() {
		using var buffer = _block.WrittenMemory.AsStream();

		for (var i = 0; i < Count; i++) {
			yield return new MemEntry(i, (long) buffer.Read<ulong>(), (long)buffer.Read<ulong>());
		}
	}

	public IEnumerable<MemEntry> ListFromEnd() {
		return ListFromEnd(Count - 1);
	}
	
	public IEnumerable<MemEntry> ListFromEnd(int start) {
		Ensure.Nonnegative(Count - 1 - start, "Starting point is greater than the length of the table");
		using var buffer = _block.WrittenMemory.AsStream();

		for (var i = start; i >= 0; --i) {
			buffer.Seek(i * MemTableEntrySize, SeekOrigin.Begin);
			yield return new MemEntry(i, (long) buffer.Read<ulong>(), (long)buffer.Read<ulong>());
		}
	}	
	
	public MemEntry ClosestGreaterOrEqualEntry(long revision, long position) {
		using var buffer = _block.WrittenMemory.AsStream();
		var entry = new MemEntry(-1, revision, position);
		var low = 0;
		var high = Count - 1;
		var closest = MemEntry.Default;
			
		while (low <= high) {
			var mid = (low + high) / 2;
			buffer.Seek(mid * MemTableEntrySize, SeekOrigin.Begin);
			var current = new MemEntry(mid, (long)buffer.Read<ulong>(), (long)buffer.Read<ulong>());
			
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
		using var buffer = _block.WrittenMemory.AsStream();
		var entry = new MemEntry(-1, revision, position);
		var low = 0;
		var high = Count - 1;
		var closest = MemEntry.Default;
			
		while (low <= high) {
			var mid = (low + high) / 2;
			buffer.Seek(mid * MemTableEntrySize, SeekOrigin.Begin);
			var current = new MemEntry(mid, (long)buffer.Read<ulong>(), (long)buffer.Read<ulong>());
			
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
}
