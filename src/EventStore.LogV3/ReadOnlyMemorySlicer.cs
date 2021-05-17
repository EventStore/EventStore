using System;

namespace EventStore.LogV3 {
	public struct ReadOnlyMemorySlicer<T> {
		public ReadOnlyMemory<T> Remaining { get; private set; }

		public ReadOnlyMemorySlicer(ReadOnlyMemory<T> source) {
			Remaining = source;
		}

		public ReadOnlyMemory<T> Slice(int length) {
			var toReturn = Remaining[..length];
			Remaining = Remaining[length..];
			return toReturn;
		}
	}
}
