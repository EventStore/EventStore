using System;
using System.Runtime.InteropServices;

namespace EventStore.LogV3 {
	public static class RawNext {
		//
		// Stream Writes
		//

		//[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		//public struct StreamReference {
		//	[FieldOffset(0)] public short ParentPartition;
		//	[FieldOffset(2)] public short Partition;
		//	[FieldOffset(4)] public int Category;
		//	[FieldOffset(8)] public long StreamNumber; //qq docs say this should be 4 bytes
		//	public const int Size = 16;
		//}

		//[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		//public struct StreamWriteHeader {
		//	//qq wait a minute, i think these two fields are supposed to _be_ (overlay) the RecordId
		//	// but the real sizes of StreamNumber and EventNumber need to be taken into account
		//	[FieldOffset(0)] public StreamReference StreamReference;
		//	[FieldOffset(StreamReference.Size)] public long StartingEventNumber; //qq in spec this is a different size
		//	[FieldOffset(24)] public short Flags;
		//	[FieldOffset(26)] public short Count;
		//	//qq we might be able to get rid of this, the event records contain their sizes so i think this is implicit
		//	// but we'd need to be able to calculate it efficiently.
		//	[FieldOffset(28)] public int MetadataSize;
		//	//qq this might already be implicit in the fields above
		//	//qq but will the other records 'EvenType' etc need to be correlated later when we have explcit apis for creating them?
		//	[FieldOffset(32)] public Guid CorrelationId;
		//	public const int Size = 48;

		//}

		//[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		//public struct EventHeader {
		//	//qq tbd: m(any) of these should go in the systemmetadata?
		//	[FieldOffset(0)] public int NegativeOffsetPrev; //qq datatype depends on how much data we can fit into a single write.
		//	[FieldOffset(4)] public int NegativeOffset; //qq datatype depends on how much data we can fit into a single write.
		//	[FieldOffset(8)] public uint EventTypeNumber;
		//	[FieldOffset(12)] public short Flags;
		//	//qq whats this - for now using this to be the size of the event so we can easily skip through the events in a writerecord
		//	[FieldOffset(16)] public int RecordSize;
		//	//qq what size should all the sizes be
		//	//qq consider bitpacking them (use three bits each to describe the length of the size)
		//	// BUT bear in mind we would need to move the sizes to the variable length portion
		//	// in which case they probably neednt be all together?
		//	//qq also the last one might be implicit from the overall size of the record.
		//	[FieldOffset(20)] public int SystemMetadataSize;
		//	[FieldOffset(24)] public int DataSize;
		//	public const int Size = 28;
		//}

		//[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		//public struct PartitionHeader {
		//	[FieldOffset(0)] public Guid PartitionTypeId;
		//	[FieldOffset(16)] public Guid ParentPartitionId;
		//	[FieldOffset(32)] public long Segment;
		//	[FieldOffset(40)] public int Reference;
		//	[FieldOffset(44)] public byte Flags;
		//	public const int Size = 45;
		//}

		//[StructLayout(LayoutKind.Explicit, Size = Size, Pack = 1)]
		//public struct EventTypeHeader {
		//	[FieldOffset(0)] public Guid PartitionId;
		//	[FieldOffset(16)] public int TypeNumber;
		//	[FieldOffset(20)] public short Version;
		//	[FieldOffset(22)] public Guid CorrelationId; //qq i think we can get rid of this
		//	public const int Size = 38;
		//}
	}


	//public static RecordView<RawNext.EventTypeHeader> CreateEventTypeRecord(
	//	DateTime timeStamp,
	//	Guid recordId,
	//	Guid correlationId,
	//	long logPosition,
	//	string eventTypeName) {

	//	var payloadLength = _utf8NoBom.GetByteCount(eventTypeName);
	//	var record = MutableRecordView<RawNext.EventTypeHeader>.Create(payloadLength);
	//	ref var header = ref record.Header;
	//	ref var subHeader = ref record.SubHeader;

	//	header.Type = subHeader.Type();
	//	header.Version = subHeader.CurrentVersion();
	//	header.TimeStamp = timeStamp;
	//	header.RecordId = recordId;
	//	header.LogPosition = logPosition;

	//	subHeader.CorrelationId = correlationId;
	//	subHeader.PartitionId = default; //qq
	//	subHeader.TypeNumber = default; //qq
	//	subHeader.Version = default; //qq

	//	PopulateString(eventTypeName, record.Payload.Span);

	//	//qq wrap in string thing
	//	return record;
	//}

	//qq database bootstraping (after the epoch)
	// - PartitionType (PartitionTypeId = ?, PartitionId = Guid.Empty; Name = "Root")
	// - Partition (Type = Root)
	// ...
}
