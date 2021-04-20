using System;
using System.Runtime.InteropServices;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;

namespace EventStore.LogV3 {
	// todo: alignment, padding (of fields and of records)
	// bear in mind that any record with variable length payload need not have padding
	// in its fixed size header.
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
		public struct EventHeader {
			[FieldOffset(0)] public int _negativeOffsetPrev; //qq datatype depends on how much data we can fit into a single write.
			[FieldOffset(4)] public int _negativeOffset; //qq datatype depends on how much data we can fit into a single write.
			[FieldOffset(8)] public Guid _eventId;
			// todo: not used yet
			[FieldOffset(24)] public int _eventTypeNumber;
			[FieldOffset(28)] public PrepareFlags _flags;
			[FieldOffset(32)] public int _eventSize;
			[FieldOffset(36)] public int _systemMetadataSize;
			[FieldOffset(40)] public int _dataSize;
			public const int Size = 44;

			public int NegativeOffsetPrev {
				get => _negativeOffsetPrev;
				set => _negativeOffsetPrev = value;
			}

			public int NegativeOffset {
				get => _negativeOffset;
				set => _negativeOffset = value;
			}

			public Guid EventId {
				get => _eventId;
				set => _eventId = value;
			}

			public int EventTypeNumber {
				get => _eventTypeNumber;
				set => _eventTypeNumber = value;
			}

			public PrepareFlags Flags {
				get => _flags;
				set => _flags = value;
			}

			public int EventSize {
				get => _eventSize;
				set => _eventSize = value;
			}

			public int SystemMetadataSize {
				get => _systemMetadataSize;
				set => _systemMetadataSize = value;
			}

			public int DataSize {
				get => _dataSize;
				set => _dataSize = value;
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
		public struct StreamHeader {
			[FieldOffset(0)] public Guid _partitionId;
			[FieldOffset(16)] public Guid _streamTypeId;
			[FieldOffset(32)] public long _referenceId;
			public const int Size = 40;

			public Guid PartitionId {
				get => _partitionId;
				set => _partitionId = value;
			}

			public Guid StreamTypeId {
				get => _streamTypeId;
				set => _streamTypeId = value;
			}

			public long ReferenceId {
				get => _referenceId;
				set => _referenceId = value;
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

		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct StreamWriteHeader {
			[FieldOffset(0)] public long _streamNumber;
			[FieldOffset(8)] public long _startingEventNumber;
			[FieldOffset(16)] public short _flags;
			[FieldOffset(18)] public short _count;
			[FieldOffset(20)] public int _metadataSize;
			public const int Size = 24;

			public long StreamNumber {
				get => _streamNumber;
				set => _streamNumber = value;
			}

			public long StartingEventNumber {
				get => _startingEventNumber;
				set => _startingEventNumber = value;
			}

			public short Flags {
				get => _flags;
				set => _flags = value;
			}

			public short Count {
				get => _count;
				set => _count = value;
			}

			public int MetadataSize {
				get => _metadataSize;
				set => _metadataSize = value;
			}
		}
	}
}
