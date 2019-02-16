using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.AllReader {
	[TestFixture]
	public class when_a_single_write_is_after_transaction_end_but_before_commit_is_present : RepeatableDbTestScenario {
		[Test]
		public void should_be_able_to_read_the_transactional_writes_when_the_commit_is_present() {
			CreateDb(Rec.TransSt(0, "transaction_stream_id"),
				Rec.Prepare(0, "transaction_stream_id"),
				Rec.TransEnd(0, "transaction_stream_id"),
				Rec.Prepare(1, "single_write_stream_id", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted));

			var firstRead = ReadIndex.ReadAllEventsForward(new Data.TFPos(0, 0), 10);

			Assert.AreEqual(1, firstRead.Records.Count);
			Assert.AreEqual("single_write_stream_id", firstRead.Records[0].Event.EventStreamId);

			CreateDb(Rec.TransSt(0, "transaction_stream_id"),
				Rec.Prepare(0, "transaction_stream_id"),
				Rec.TransEnd(0, "transaction_stream_id"),
				Rec.Prepare(1, "single_write_stream_id", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
				Rec.Commit(0, "transaction_stream_id"));

			var transactionRead = ReadIndex.ReadAllEventsForward(firstRead.NextPos, 10);

			Assert.AreEqual(1, transactionRead.Records.Count);
			Assert.AreEqual("transaction_stream_id", transactionRead.Records[0].Event.EventStreamId);
		}
	}
}
