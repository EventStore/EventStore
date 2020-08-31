using EventStore.Core.Tests.TransactionLogV2.Scavenging.Helpers;
using EventStore.Core.TransactionLogV2.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLogV2.Scavenging {
	[TestFixture]
	public class when_having_nothing_to_scavenge : ScavengeTestScenario {
		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Commit(0, "bla"))
				.Chunk(Rec.Prepare(2, "bla3"),
					Rec.Prepare(2, "bla3"),
					Rec.Commit(1, "bla"),
					Rec.Commit(2, "bla3"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return dbResult.Recs;
		}

		[Test]
		public void all_records_are_kept_untouched() {
			CheckRecords();
		}
	}
}
