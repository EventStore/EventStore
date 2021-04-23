using System;
using System.Buffers;

namespace EventStore.LogV3 {
	public static class MemoryExtensions {
		public static MemoryWriter<T> Writer<T>(this Memory<T> memory) =>
			new MemoryWriter<T>(memory);

		public static MemorySlicer<T> Slicer<T>(this Memory<T> memory) =>
			new MemorySlicer<T>(memory);

		public static ReadOnlyMemorySlicer<T> Slicer<T>(this ReadOnlyMemory<T> memory) =>
			new ReadOnlyMemorySlicer<T>(memory);
	}

	public struct MemorySlicer<T> {
		public Memory<T> Remaining { get; private set; }

		public MemorySlicer(Memory<T> source) {
			Remaining = source;
		}

		public Memory<T> Slice(int length) {
			var toReturn = Remaining[..length];
			Remaining = Remaining[length..];
			return toReturn;
		}
	}

	public struct MemoryWriter<T> : IBufferWriter<T> {
		private Memory<T> _theMemory;
		private int _consumed;

		public MemoryWriter(Memory<T> theMemory) {
			_theMemory = theMemory;
			_consumed = 0;
		}


		public void Advance(int count) {
			_consumed += count;
		}

		public Memory<T> GetMemory(int sizeHint) {
			return _theMemory[_consumed..];
		}

		public Span<T> GetSpan(int sizeHint) {
			return _theMemory.Span[_consumed..];
		}
	}
}
