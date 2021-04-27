using System;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Chaser {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_chaser_reads_prepare_event<TLogFormat, TStreamId> : with_storage_chaser_service<TLogFormat, TStreamId> {
		private Guid _eventId;
		private Guid _transactionId;

		public override void When() {
			_eventId = Guid.NewGuid();
			_transactionId = Guid.NewGuid();

			var logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormat;
			logFormat.StreamNameIndex.GetOrAddId("WorldEnding", out var streamId);

			var record = LogRecord.Prepare(
				factory: logFormat.RecordFactory,
				logPosition: 0,
				eventId: _eventId,
				correlationId: _transactionId,
				transactionPos: 0,
				transactionOffset: 0xBEEF,
				eventStreamId: streamId,
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
			AssertEx.IsOrBecomesTrue(() => PrepareAcks.Count == 1, msg: "PrepareAck msg not received");
			Assert.True(PrepareAcks.TryDequeue(out var prepareAck));
			Assert.AreEqual(_transactionId, prepareAck.CorrelationId);

		}

	}
}
