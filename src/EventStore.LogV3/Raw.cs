using System;
using System.Runtime.InteropServices;
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
			// could reduce the resolution of the timestamp and fit it in 6 bytes?
			[FieldOffset(8)] private long _ticks;
			[FieldOffset(RecordIdOffset)] private Guid _recordId;

			// todo: remove this if possible
			[FieldOffset(RecordIdOffset + RecordIdSize)] private long _logPosition;
			public const int Size = 40;
			public const int RecordIdOffset = 16;
			public const int RecordIdSize = 16;

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

		[StructLayout(LayoutKind.Explicit, Size = RecordHeader.RecordIdSize, Pack = 1)]
		public struct StreamWriteId {
			[FieldOffset(0)] private ushort _topicNumber;
			[FieldOffset(2)] private ushort _categoryNumber;
			[FieldOffset(4)] private uint _streamNumber;
			// todo [FieldOffset(8)] private ushort _parentTopicNumber;
			[FieldOffset(8)] private long _startingEventNumber;

			public ushort TopicNumber {
				get => _topicNumber;
				set => _topicNumber = value;
			}

			public ushort CategoryNumber {
				get => _categoryNumber;
				set => _categoryNumber = value;
			}

			public uint StreamNumber {
				get => _streamNumber;
				set => _streamNumber = value;
			}

			public long StartingEventNumber {
				get => _startingEventNumber;
				set => _startingEventNumber = value;
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

		[Flags]
		public enum EventFlags : ushort {
		}

		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct EventHeader {
			// todo: not used yet
			[FieldOffset(0)] private int _eventTypeNumber;
			[FieldOffset(4)] private EventFlags _flags;
			[FieldOffset(8)] private int _eventSize;
			[FieldOffset(16)] private int _systemMetadataSize;
			[FieldOffset(20)] private int _dataSize;
			public const int Size = 24;

			public int EventTypeNumber {
				get => _eventTypeNumber;
				set => _eventTypeNumber = value;
			}

			public EventFlags Flags {
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
		public struct PartitionHeader {
			[FieldOffset(0)] private Guid _partitionTypeId;
			[FieldOffset(16)] private Guid _parentPartitionId;
			[FieldOffset(32)] private byte _flags;
			public const int Size = 33;

			public Guid PartitionTypeId {
				get => _partitionTypeId;
				set => _partitionTypeId = value;
			}
			
			public Guid ParentPartitionId {
				get => _parentPartitionId;
				set => _parentPartitionId = value;
			}
			
			public byte Flags {
				get => _flags;
				set => _flags = value;
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
		
		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct EventTypeHeader {
			[FieldOffset(0)] private Guid _partitionId;
			[FieldOffset(16)] private uint _referenceNumber;
			[FieldOffset(20)] private ushort _version;
			
			public const int Size = 22;

			public Guid PartitionId {
				get => _partitionId;
				set => _partitionId = value;
			}
			
			public uint ReferenceNumber {
				get => _referenceNumber;
				set => _referenceNumber = value;
			}

			public ushort Version {
				get => _version;
				set => _version = value;
			}
		}

		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct ContentTypeHeader {
			[FieldOffset(0)] private Guid _partitionId;
			[FieldOffset(16)] private ushort _referenceNumber;

			public const int Size = 18;

			public Guid PartitionId {
				get => _partitionId;
				set => _partitionId = value;
			}

			public ushort ReferenceNumber {
				get => _referenceNumber;
				set => _referenceNumber = value;
			}
		}

		[Flags]
		public enum StreamWriteFlags : ushort {
		}

		[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		public struct StreamWriteHeader {
			[FieldOffset(0)] private StreamWriteFlags _flags;
			[FieldOffset(2)] private short _count;
			[FieldOffset(4)] private int _metadataSize;
			public const int Size = 8;

			public StreamWriteFlags Flags {
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
