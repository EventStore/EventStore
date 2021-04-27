using System;
using System.Runtime.InteropServices;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using Xunit;

namespace EventStore.LogV3.Tests {
	public class RecordCreatorTests {
		readonly Guid _guid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
		readonly Guid _guid2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
		readonly Guid _guid3 = Guid.Parse("00000000-0000-0000-0000-000000000003");
		readonly DateTime _dateTime1 = new DateTime(2020, 01, 01, 01, 01, 01);
		readonly long _long1 = 1;
		readonly long _long2 = 2;
		readonly long _long3 = 3;
		readonly string _string1 = "one";
		readonly PrepareFlags _prepareflags = PrepareFlags.SingleWrite;
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
		public void can_create_stream_record() {
			var record = RecordCreator.CreateStreamRecord(
				streamId: _guid1,
				timeStamp: _dateTime1,
				logPosition: _long1,
				streamNumber: _long2,
				streamName: _string1,
				partitionId: _guid2,
				streamTypeId: _guid3);

			Assert.Equal(LogRecordType.Stream, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV0, record.Header.Version);
			Assert.Equal(_guid1, record.Header.RecordId);
			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_long1, record.Header.LogPosition);
			Assert.Equal(_guid2, record.SubHeader.PartitionId);
			Assert.Equal(_long2, record.SubHeader.ReferenceId);
			Assert.Equal(_guid3, record.SubHeader.StreamTypeId);
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
		public void can_create_stream_write_record_for_single_event() {
			var record = RecordCreator.CreateStreamWriteRecordForSingleEvent(
				timeStamp: _dateTime1,
				recordId: _guid1,
				logPosition: _long1,
				streamNumber: _long2,
				startingEventNumber: _long3,
				recordMetadata: _bytes1.Span,
				eventId: _guid2,
				eventSystemMetadata: _bytes2.Span,
				eventData: _bytes3.Span,
				eventMetadata: _bytes4.Span,
				eventFlags: _prepareflags);

			Assert.Equal(LogRecordType.LogV3StreamWrite, record.Header.Type);
			Assert.Equal(LogRecordVersion.LogRecordV0, record.Header.Version);
			Assert.Equal(_dateTime1, record.Header.TimeStamp);
			Assert.Equal(_guid1, record.Header.RecordId);
			Assert.Equal(_long1, record.Header.LogPosition);
			Assert.Equal(_long2, record.SubHeader.StreamNumber);
			Assert.Equal(_long3, record.SubHeader.StartingEventNumber);
			Assert.Equal(MemoryMarshal.ToEnumerable(_bytes1), MemoryMarshal.ToEnumerable(record.SystemMetadata));
			Assert.Equal(_guid2, record.Event.Header.EventId);
			Assert.Equal(MemoryMarshal.ToEnumerable(_bytes2), MemoryMarshal.ToEnumerable(record.Event.SystemMetadata));
			Assert.Equal(MemoryMarshal.ToEnumerable(_bytes3), MemoryMarshal.ToEnumerable(record.Event.Data));
			Assert.Equal(MemoryMarshal.ToEnumerable(_bytes4), MemoryMarshal.ToEnumerable(record.Event.Metadata));
			Assert.Equal(_prepareflags, record.Event.Header.Flags);
		}
	}
}
