﻿using System;

namespace EventStore.LogV3 {
	public static class MemoryExtensions {
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
}
