using System;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Chaser {
	[TestFixture]
	public class when_chaser_reads_prepare_event : with_storage_chaser_service {
		private Guid _eventId;
		private Guid _transactionId;

		public override void When() {
			_eventId = Guid.NewGuid();
			_transactionId = Guid.NewGuid();
			var record = new PrepareLogRecord(logPosition: 0,
				eventId: _eventId,
				correlationId: _transactionId,
				transactionPosition: 0xDEAD,
				transactionOffset: 0xBEEF,
				eventStreamId: "WorldEnding",
				expectedVersion: 1234,
				timeStamp: new DateTime(2012, 12, 21),
				flags: PrepareFlags.SingleWrite,
				eventType: "type",
				data: new byte[] { 1, 2, 3, 4, 5 },
				metadata: new byte[] { 7, 17 });



			Assert.True(Writer.Write(record, out _));
			Writer.Flush();
		}
		[Test]
		public void prepare_ack_should_be_published() {
			AssertEx.IsOrBecomesTrue(() => PrepareAcks.Count == 1, msg: "PrepareAck msg not recieved");
			var prepareAck = PrepareAcks[0];
			Assert.AreEqual(_transactionId, prepareAck.CorrelationId);

		}

	}
}
