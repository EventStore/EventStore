using System;
using System.Runtime.InteropServices;
using EventStore.LogCommon;
using Xunit;

namespace EventStore.LogV3.Tests {
	public class RecordCreatorTests {
		readonly Guid _guid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
		readonly Guid _guid2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
		readonly Guid _guid3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
		readonly DateTime _dateTime1 = new DateTime(2020, 01, 01, 01, 01, 01);
		readonly int _int1 = 1;
		readonly long _long1 = 1;
		readonly long _long2 = 2;
		readonly long _long3 = 3;
		readonly long _long4 = 4;
		readonly uint _uint1 = 5;
		readonly ushort _ushort1 = 6;
		readonly byte _byte1 = 8;
		readonly string _string1 = "one";
		readonly Raw.EventFlags _prepareflags = (Raw.EventFlags)3;
		readonly ReadOnlyMemory<byte> _bytes1 = new byte[] { 0x10 };
		readonly ReadOnlyMemory<byte> _bytes2 = new byte[] { 0x20, 0x01 };
		readonly ReadOnlyMemory<byte> _bytes3 = new byte[] { 0x30, 0x01, 0x01 };
		readonly ReadOnlyMemory<byte> _bytes4 = new byte[] { 0x40, 0x01, 0x01, 0x01 };

		[Fact]
		public void can_create_epoch() {
			var epochNumber = 5;
			var record = RecordCreator.CreateEpochRecord(
				timeStamp: _dateTime1,
				logPosition: _long1,
				epochNumber: epochNumber,
				epochId: _guid1,
				prevEpochPosition: _long2,
				leaderInstanceId: _guid2);

			Assert.Equal(LogRecordType.System, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV1, record.Header.Version);

			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_guid1, record.Header.RecordId);
			Assert.Equal(_long1, record.Header.LogPosition);

			Assert.Equal(epochNumber, record.SubHeader.EpochNumber);
			Assert.Equal(_guid2, record.SubHeader.LeaderInstanceId);
			Assert.Equal(_long2, record.SubHeader.PrevEpochPosition);
		}
		
		[Fact]
		public void can_create_partition_record() {
			var record = RecordCreator.CreatePartitionRecord(
				timeStamp: _dateTime1,
				logPosition: _long1,
				partitionId: _guid1,
				partitionTypeId: _guid2,
				parentPartitionId: _guid3,
				flags: _byte1,
				name: _string1);

			Assert.Equal(LogRecordType.Partition, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV0, record.Header.Version);
			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_guid1, record.Header.RecordId);
			Assert.Equal(_long1, record.Header.LogPosition);
			Assert.Equal(_guid2, record.SubHeader.PartitionTypeId);
			Assert.Equal(_guid3, record.SubHeader.ParentPartitionId);
			Assert.Equal(_byte1, record.SubHeader.Flags);
			Assert.Equal(_string1, record.StringPayload);
		}
		
		[Fact]
		public void can_create_partition_type_record() {
			var record = RecordCreator.CreatePartitionTypeRecord(
				timeStamp: _dateTime1,
				logPosition: _long1,
				partitionTypeId: _guid1,
				partitionId: _guid2,
				name: _string1);

			Assert.Equal(LogRecordType.PartitionType, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV0, record.Header.Version);
			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_guid1, record.Header.RecordId);
			Assert.Equal(_long1, record.Header.LogPosition);
			Assert.Equal(_guid2, record.SubHeader.PartitionId);
			Assert.Equal(_string1, record.StringPayload);
		}

		[Fact]
		public void can_create_stream_type_record() {
			var record = RecordCreator.CreateStreamTypeRecord(
				timeStamp: _dateTime1,
				logPosition: _long1,
				streamTypeId: _guid1,
				partitionId: _guid2,
				name: _string1);

			Assert.Equal(LogRecordType.StreamType, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV0, record.Header.Version);
			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_guid1, record.Header.RecordId);
			Assert.Equal(_long1, record.Header.LogPosition);
			Assert.Equal(_guid2, record.SubHeader.PartitionId);
			Assert.Equal(_string1, record.StringPayload);
		}
		
		[Fact]
		public void can_create_event_type_record() {
			var record = RecordCreator.CreateEventTypeRecord(
				timeStamp: _dateTime1,
				logPosition: _long1,
				eventTypeId: _guid1,
				partitionId: _guid2,
				referenceNumber: _uint1,
				version: _ushort1,
				name: _string1);

			Assert.Equal(LogRecordType.EventType, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV0, record.Header.Version);
			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_guid1, record.Header.RecordId);
			Assert.Equal(_long1, record.Header.LogPosition);
			Assert.Equal(_guid2, record.SubHeader.PartitionId);
			Assert.Equal(_uint1, record.SubHeader.ReferenceNumber);
			Assert.Equal(_ushort1, record.SubHeader.Version);
			Assert.Equal(_string1, record.StringPayload);
		}
		
		[Fact]
		public void can_create_content_type_record() {
			var record = RecordCreator.CreateContentTypeRecord(
				timeStamp: _dateTime1,
				logPosition: _long1,
				contentTypeId: _guid1,
				partitionId: _guid2,
				referenceNumber: _ushort1,
				name: _string1);

			Assert.Equal(LogRecordType.ContentType, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV0, record.Header.Version);
			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_guid1, record.Header.RecordId);
			Assert.Equal(_long1, record.Header.LogPosition);
			Assert.Equal(_guid2, record.SubHeader.PartitionId);
			Assert.Equal(_ushort1, record.SubHeader.ReferenceNumber);
			Assert.Equal(_string1, record.StringPayload);
		}

		[Fact]
		public void can_create_stream_write_record_for_single_event() {
			var record = RecordCreator.CreateStreamWriteRecordForSingleEvent(
				timeStamp: _dateTime1,
				correlationId: _guid1,
				logPosition: _long1,
				transactionPosition: _long2,
				transactionOffset: _int1,
				streamNumber: _long3,
				startingEventNumber: _long4,
				eventId: _guid2,
				eventType: _string1,
				eventData: _bytes3.Span,
				eventMetadata: _bytes4.Span,
				eventFlags: _prepareflags);

			Assert.Equal(LogRecordType.StreamWrite, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV0, record.Header.Version);
			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_long1, record.Header.LogPosition);
			Assert.Equal<Guid>(_guid1, record.SystemMetadata.CorrelationId);
			Assert.Equal(_long2, record.SystemMetadata.TransactionPosition);
			Assert.Equal(_int1, record.SystemMetadata.TransactionOffset);
			Assert.Equal(0, record.WriteId.CategoryNumber);
			Assert.Equal(0, record.WriteId.TopicNumber);
			Assert.Equal(_long3, record.WriteId.StreamNumber);
			Assert.Equal(_long4, record.WriteId.StartingEventNumber);
			Assert.Equal<Guid>(_guid2, record.Event.SystemMetadata.EventId);
			Assert.Equal(_string1, record.Event.SystemMetadata.EventType);
			Assert.Equal(MemoryMarshal.ToEnumerable(_bytes3), MemoryMarshal.ToEnumerable(record.Event.Data));
			Assert.Equal(MemoryMarshal.ToEnumerable(_bytes4), MemoryMarshal.ToEnumerable(record.Event.Metadata));
			Assert.Equal(_prepareflags, record.Event.Header.Flags);
		}
	}
}
