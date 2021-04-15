using System;

namespace EventStore.LogV3 {
	public struct ReadOnlySlicedRecord {
		public ReadOnlyMemory<byte> Bytes { get; init; }
		public ReadOnlyMemory<byte> HeaderMemory { get; init; }
		public ReadOnlyMemory<byte> SubHeaderMemory { get; init; }
		public ReadOnlyMemory<byte> PayloadMemory { get; init; }
	}
}
