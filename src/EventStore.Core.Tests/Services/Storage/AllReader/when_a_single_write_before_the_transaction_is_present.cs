using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.AllReader {
	[TestFixture]
	public class when_a_single_write_before_the_transaction_is_present : RepeatableDbTestScenario {
		[Test]
		public void should_be_able_to_read_the_transactional_writes_when_the_commit_is_present() {
			CreateDb(
				Rec.Prepare(0, "single_write_stream_id_1", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
				Rec.TransSt(1, "transaction_stream_id"),
				Rec.Prepare(1, "transaction_stream_id"),
				Rec.TransEnd(1, "transaction_stream_id"),
				Rec.Prepare(2, "single_write_stream_id_2", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
				Rec.Prepare(3, "single_write_stream_id_3", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
				Rec.Prepare(4, "single_write_stream_id_4", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted));

			var firstRead = ReadIndex.ReadAllEventsForward(new Data.TFPos(0, 0), 10);

			Assert.AreEqual(4, firstRead.Records.Count);
			Assert.AreEqual("single_write_stream_id_1", firstRead.Records[0].Event.EventStreamId);
			Assert.AreEqual("single_write_stream_id_2", firstRead.Records[1].Event.EventStreamId);
			Assert.AreEqual("single_write_stream_id_3", firstRead.Records[2].Event.EventStreamId);
			Assert.AreEqual("single_write_stream_id_4", firstRead.Records[3].Event.EventStreamId);

			CreateDb(
				Rec.Prepare(0, "single_write_stream_id_1", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
				Rec.TransSt(1, "transaction_stream_id"),
				Rec.Prepare(1, "transaction_stream_id"),
				Rec.TransEnd(1, "transaction_stream_id"),
				Rec.Prepare(2, "single_write_stream_id_2", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
				Rec.Prepare(3, "single_write_stream_id_3", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
				Rec.Prepare(4, "single_write_stream_id_4", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
				Rec.Commit(1, "transaction_stream_id"));

			var transactionRead = ReadIndex.ReadAllEventsForward(firstRead.NextPos, 10);

			Assert.AreEqual(1, transactionRead.Records.Count);
			Assert.AreEqual("transaction_stream_id", transactionRead.Records[0].Event.EventStreamId);
		}
	}
}
