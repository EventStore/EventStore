using System;
using System.Runtime.InteropServices;
using EventStore.LogCommon;

namespace EventStore.LogV3 {
	// todo: alignment, padding (of fields and of records)
	public static class Raw {
		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct RecordHeader {
			[FieldOffset(0)] private LogRecordType _type;
			[FieldOffset(1)] private byte _version;
			// todo: too much padding for somethng that will occur every record
			[FieldOffset(8)] private long _ticks;
			[FieldOffset(16)] private Guid _recordId;

			// todo: remove this if possible
			[FieldOffset(48)] private long _logPosition;
			public const int Size = 56;

			public LogRecordType Type {
				get => _type;
				set => _type = value;
			}

			public byte Version {
				get => _version;
				set => _version = value;
			}

			public DateTime TimeStamp {
				get => new DateTime(_ticks);
				set => _ticks = value.Ticks;
			}

			public Guid RecordId {
				get => _recordId;
				set => _recordId = value;
			}

			public long LogPosition {
				get => _logPosition;
				set => _logPosition = value;
			}
		}

		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct EpochHeader {
			[FieldOffset(0)] private Guid _leaderInstanceId;
			[FieldOffset(16)] private long _prevEpochPosition;
			[FieldOffset(24)] private int _epochNumber;
			public const int Size = 28;

			public Guid LeaderInstanceId {
				get => _leaderInstanceId;
				set => _leaderInstanceId = value;
			}

			public long PrevEpochPosition {
				get => _prevEpochPosition;
				set => _prevEpochPosition = value;
			}

			public int EpochNumber {
				get => _epochNumber;
				set => _epochNumber = value;
			}
		}

		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct PartitionTypeHeader {
			[FieldOffset(0)] private Guid _partitionId;
			public const int Size = 16;

			public Guid PartitionId {
				get => _partitionId;
				set => _partitionId = value;
			}
		}

		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct StreamTypeHeader {
			[FieldOffset(0)] public Guid _partitionId;
			public const int Size = 16;

			public Guid PartitionId {
				get => _partitionId;
				set => _partitionId = value;
			}
		}
	}
}
