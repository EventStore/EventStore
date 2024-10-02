// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

namespace EventStore.Core.DataStructures.ProbabilisticFilter {
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
