using System;
using EventStore.Core.LogAbstraction;
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
		//readonly long _long3 = 3;
		//readonly int _int100 = 100;
		//readonly string _string1 = "one";
		//readonly string _string2 = "two";
		readonly LogV3RecordFactory _sut = new(LogFormatAbstractor.V2.RecordFactory);
		//readonly PrepareFlags _prepareflags = PrepareFlags.SingleWrite;
		readonly ReadOnlyMemory<byte> _bytes1 = new byte[10];
		readonly ReadOnlyMemory<byte> _bytes2 = new byte[10];

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
	}
}
