using System;
using System.Runtime.InteropServices;
using EventStore.Core.LogV3;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using Xunit;

namespace EventStore.Core.Tests.XUnit.LogV3 {
	// we dont want too many tests here, rather pass this factory into the higher up
	// integration tests like we do for the v2 factory.
	public class LogV3RecordFactoryTests {
		readonly Guid _guid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
		readonly Guid _guid2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
		readonly DateTime _dateTime1 = new DateTime(2020, 01, 01, 01, 01, 01);
		readonly long _long1 = 1;
		readonly long _long2 = 2;
		readonly long _long3 = 3;
		readonly long _long4 = 4;
		readonly string _string1 = "one";
		//readonly string _string2 = "two";
		readonly LogV3RecordFactory _sut = new();
		readonly PrepareFlags _prepareflags = PrepareFlags.SingleWrite;
		readonly ReadOnlyMemory<byte> _bytes1 = new byte[] { 0x10 }; 
		readonly ReadOnlyMemory<byte> _bytes2 = new byte[] { 0x20, 0x01 };

		[Fact]
		public void can_create_epoch() {
			var epochNumber = 5;
			var epochIn = new EpochRecord(
				epochPosition: _long1,
				epochNumber: epochNumber,
				epochId: _guid1,
				prevEpochPosition: _long2,
				timeStamp: _dateTime1,
				leaderInstanceId: _guid2);

			var systemEpoch = _sut.CreateEpoch(epochIn);

			var epochOut = systemEpoch.GetEpochRecord();

			Assert.Equal(_long1, systemEpoch.LogPosition);
			Assert.Equal(LogRecordType.System, systemEpoch.RecordType);
			Assert.Equal(SystemRecordType.Epoch, systemEpoch.SystemRecordType);
			Assert.Equal(_long1, systemEpoch.Version);

			Assert.Equal(epochIn.EpochPosition, epochOut.EpochPosition);
			Assert.Equal(epochIn.TimeStamp, epochOut.TimeStamp);
			Assert.Equal(epochIn.EpochId, epochOut.EpochId);
			Assert.Equal(epochIn.EpochNumber, epochOut.EpochNumber);
			Assert.Equal(epochIn.PrevEpochPosition, epochOut.PrevEpochPosition);
			Assert.Equal(epochIn.LeaderInstanceId, epochOut.LeaderInstanceId);
		}

		[Fact]
		public void can_create_prepare() {
			var prepare = _sut.CreatePrepare(
				logPosition: _long1,
				correlationId: _guid1,
				eventId: _guid2,
				// must be same as logPosition
				transactionPosition: _long1,
				// must be 0 since only one event is supported at the moment
				transactionOffset: 0,
				eventStreamId: _long3,
				expectedVersion: _long4,
				timeStamp: _dateTime1,
				flags: _prepareflags,
				eventType: _string1,
				data: _bytes1,
				metadata: _bytes2);

			Assert.Equal(_long1, prepare.LogPosition);
			Assert.Equal(_guid1, prepare.CorrelationId);
			Assert.Equal(_guid2, prepare.EventId);
			Assert.Equal(_long1, prepare.TransactionPosition);
			Assert.Equal(0, prepare.TransactionOffset);
			Assert.Equal(_long3, prepare.EventStreamId);
			Assert.Equal(_long4, prepare.ExpectedVersion);
			Assert.Equal(_dateTime1, prepare.TimeStamp);
			Assert.Equal(_prepareflags, prepare.Flags);
			Assert.Equal(_string1, prepare.EventType);
			Assert.Equal(MemoryMarshal.ToEnumerable(_bytes1), MemoryMarshal.ToEnumerable(prepare.Data));
			Assert.Equal(MemoryMarshal.ToEnumerable(_bytes2), MemoryMarshal.ToEnumerable(prepare.Metadata));
		}
	}
}
