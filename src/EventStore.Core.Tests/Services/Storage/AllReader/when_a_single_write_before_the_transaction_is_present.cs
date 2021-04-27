using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.AllReader {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long), Ignore = "Explicit transactions are not supported yet by Log V3")]
	public class when_a_single_write_before_the_transaction_is_present<TLogFormat, TStreamId> : RepeatableDbTestScenario<TLogFormat, TStreamId> {
		[Test]
		public void should_be_able_to_read_the_transactional_writes_when_the_commit_is_present() {
			CreateDb(
				Rec.Prepare("single_write_stream_id_1"),
				Rec.TransStart(1, "transaction_stream_id"),
				Rec.TransPrepare(1, "transaction_stream_id"),
				Rec.TransEnd(1, "transaction_stream_id"),
				Rec.Prepare("single_write_stream_id_2"),
				Rec.Prepare("single_write_stream_id_3"),
				Rec.Prepare("single_write_stream_id_4"));

			var firstRead = ReadIndex.ReadAllEventsForward(new Data.TFPos(0, 0), 10);

			Assert.AreEqual(4, firstRead.Records.Count);
			Assert.AreEqual("single_write_stream_id_1", firstRead.Records[0].Event.EventStreamId);
			Assert.AreEqual("single_write_stream_id_2", firstRead.Records[1].Event.EventStreamId);
			Assert.AreEqual("single_write_stream_id_3", firstRead.Records[2].Event.EventStreamId);
			Assert.AreEqual("single_write_stream_id_4", firstRead.Records[3].Event.EventStreamId);

			CreateDb(
				Rec.Prepare("single_write_stream_id_1"),
				Rec.TransStart(1, "transaction_stream_id"),
				Rec.TransPrepare(1, "transaction_stream_id"),
				Rec.TransEnd(1, "transaction_stream_id"),
				Rec.Prepare("single_write_stream_id_2"),
				Rec.Prepare("single_write_stream_id_3"),
				Rec.Prepare("single_write_stream_id_4"),
				Rec.TransCommit(1, "transaction_stream_id"));

			var transactionRead = ReadIndex.ReadAllEventsForward(firstRead.NextPos, 10);

			Assert.AreEqual(1, transactionRead.Records.Count);
			Assert.AreEqual("transaction_stream_id", transactionRead.Records[0].Event.EventStreamId);
		}
	}
}
