using System;
using EventStore.LogCommon;
using Xunit;

namespace EventStore.LogV3.Tests {
	public class RecordCreatorTests {
		readonly Guid _guid1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
		readonly Guid _guid2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
		readonly DateTime _dateTime1 = new DateTime(2020, 01, 01, 01, 01, 01);
		readonly long _long1 = 1;
		readonly long _long2 = 2;

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
	}
}
