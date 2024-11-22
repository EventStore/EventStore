using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.AllReader {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint), Ignore = "Explicit transactions are not supported yet by Log V3")]
	public class when_reading_all_with_filtering_and_transactions<TLogFormat, TStreamId>
		: RepeatableDbTestScenario<TLogFormat, TStreamId> {

		[Test]
		public void should_receive_all_events_forward() {
			// create a db with explicit transactions, some of which are filtered out on read.
			// previously, a bug caused those filtered-out records to prevent the successful
			// reading of subsequent events that are contained within an explicit transaction.

			static Rec[] ExplicitTransaction(int transaction, string stream) => new[] {
				Rec.TransSt(transaction, stream),
				Rec.Prepare(transaction, stream),
				Rec.TransEnd(transaction, stream),
				Rec.Commit(transaction, stream),
			};

			var i = 0;
			CreateDb(Array.Empty<Rec>()
				.Concat(ExplicitTransaction(i++, "excludedStream"))
				.Concat(ExplicitTransaction(i++, "includedStream0"))
				.Concat(ExplicitTransaction(i++, "includedStream1"))
				.Concat(ExplicitTransaction(i++, "includedStream2"))
				.Concat(ExplicitTransaction(i++, "includedStream3"))
				.Concat(ExplicitTransaction(i++, "includedStream4"))
				.Concat(ExplicitTransaction(i++, "includedStream5"))
				.Concat(ExplicitTransaction(i++, "includedStream6"))
				.Concat(ExplicitTransaction(i++, "includedStream7"))
				.Concat(ExplicitTransaction(i++, "includedStream8"))
				.Concat(ExplicitTransaction(i++, "includedStream9"))
				.ToArray());

			var read = ReadIndex.ReadAllEventsForwardFiltered(
				pos: new Data.TFPos(0, 0),
				maxCount: 10,
				maxSearchWindow: int.MaxValue,
				eventFilter: EventFilter.StreamName.Prefixes(false, "included"),
				tracker: ITransactionFileTracker.NoOp);

			Assert.AreEqual(10, read.Records.Count);
			for (int j = 0; j < 10; j++)
				Assert.AreEqual($"includedStream{j}", read.Records[j].Event.EventStreamId);
		}

		[Test]
		public void should_receive_all_events_backward() {
			// create a db with explicit transactions, some of which are filtered out on read.
			// previously, a bug caused those filtered-out records to prevent the successful
			// reading of subsequent events that are contained within an explicit transaction.

			static Rec[] ExplicitTransaction(int transaction, string stream) => new[] {
				Rec.TransSt(transaction, stream),
				Rec.Prepare(transaction, stream),
				Rec.TransEnd(transaction, stream),
				Rec.Commit(transaction, stream),
			};

			var i = 0;
			CreateDb(Array.Empty<Rec>()
				.Concat(ExplicitTransaction(i++, "includedStream0"))
				.Concat(ExplicitTransaction(i++, "includedStream1"))
				.Concat(ExplicitTransaction(i++, "includedStream2"))
				.Concat(ExplicitTransaction(i++, "includedStream3"))
				.Concat(ExplicitTransaction(i++, "includedStream4"))
				.Concat(ExplicitTransaction(i++, "includedStream5"))
				.Concat(ExplicitTransaction(i++, "includedStream6"))
				.Concat(ExplicitTransaction(i++, "includedStream7"))
				.Concat(ExplicitTransaction(i++, "includedStream8"))
				.Concat(ExplicitTransaction(i++, "includedStream9"))
				.Concat(ExplicitTransaction(i++, "excludedStream"))
				.ToArray());

			var writerCp = DbRes.Db.Config.WriterCheckpoint.Read();
			var read = ReadIndex.ReadAllEventsBackwardFiltered(
				pos: new TFPos(writerCp, writerCp),
				maxCount: 10,
				maxSearchWindow: int.MaxValue,
				eventFilter: EventFilter.StreamName.Prefixes(false, "included"),
				tracker: ITransactionFileTracker.NoOp);

			Assert.AreEqual(10, read.Records.Count);
			for (int j = 9; j <= 0; j--)
				Assert.AreEqual($"includedStream{j}", read.Records[j].Event.EventStreamId);
		}
	}
}
