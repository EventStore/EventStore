﻿namespace EventStore.Core.DataStructures.ProbabilisticFilter {
	public static class ByteExtensions {
		public static bool IsBitSet(this byte x, long bitIndex) {
			return (x & (1 << (int)(7 - bitIndex))) != 0;
		}

		public static byte SetBit(this byte x, long bitIndex) {
			return (byte)(x | (1 << (int)(7 - bitIndex)));
		}

		public static byte UnsetBit(this byte x, long bitIndex) {
			return (byte)(x & ~(1 << (int)(7 - bitIndex)));
		}
	}
}
