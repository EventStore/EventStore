using System;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Chaser {
	[TestFixture]
	public class when_chaser_reads_system_event : with_storage_chaser_service {
		private long _logPosition;
		private Guid _epochId;
		private int _epochNumber;

		public override void When() {
			_epochId = Guid.NewGuid();
			_epochNumber = 7;
			var epoch = new EpochRecord(0, _epochNumber, _epochId, -1, DateTime.UtcNow);
			var rec = new SystemLogRecord(epoch.EpochPosition, epoch.TimeStamp, SystemRecordType.Epoch,
				SystemRecordSerialization.Json, epoch.AsSerialized());

			Assert.True(Writer.Write(rec, out _logPosition));
			Writer.Flush();
		}
		[Test]
		public void log_written_should_be_published() {
			Assert.Fail("Fix Test");
			//AssertEx.IsOrBecomesTrue(() => LogWrittenTos.Count == 1, msg: "LogWrittenTo msg not recieved");
			//var writtenTo = LogWrittenTos[0];
			//Assert.AreEqual(_logPosition, writtenTo.LogPosition);
		}
		[Test]
		public void epoch_should_be_updated() {
			AssertEx.IsOrBecomesTrue(() => EpochManager.GetLastEpoch() != null);
			Assert.AreEqual(_epochId, EpochManager.GetLastEpoch().EpochId);
			Assert.AreEqual(_epochNumber, EpochManager.GetLastEpoch().EpochNumber);
		}
	}
}
