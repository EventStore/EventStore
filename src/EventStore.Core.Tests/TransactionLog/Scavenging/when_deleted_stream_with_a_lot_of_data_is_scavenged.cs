using System.Linq;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture]
	public class when_deleted_stream_with_a_lot_of_data_is_scavenged : ScavengeTestScenario {
		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "bla"),
					Rec.Prepare(0, "bla"),
					Rec.Commit(0, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Commit(1, "bla"),
					Rec.Delete(2, "bla"),
					Rec.Commit(2, "bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => new[] {8, 9}.Contains(i)).ToArray(),
			};
		}

		[Test]
		public void only_delete_tombstone_records_with_their_commits_are_kept() {
			CheckRecords();
		}
	}

	[TestFixture]
	public class when_deleted_stream_with_a_lot_of_data_is_scavenged_with_ingore_harddelete : ScavengeTestScenario {
		protected override bool UnsafeIgnoreHardDelete() {
			return true;
		}

		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "bla"),
					Rec.Prepare(0, "bla"),
					Rec.Commit(0, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Commit(1, "bla"),
					Rec.Delete(2, "bla"),
					Rec.Commit(2, "bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => new int[] { }.Contains(i)).ToArray(),
			};
		}

		[Test]
		public void only_delete_tombstone_records_with_their_commits_are_kept() {
			CheckRecords();
		}
	}
}
