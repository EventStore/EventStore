using System.Linq;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_deleted_stream_with_a_lot_of_data_is_scavenged_log_v3<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "bla"),
					Rec.Prepare(0, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Delete(2, "bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			var keep = new int[] { 0, 7 }; // "bla" created

			return new[] {
				dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray(),
			};
		}

		[Test]
		public void only_delete_tombstone_records_with_their_commits_are_kept() {
			CheckRecords();
		}
	}

	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_deleted_stream_with_a_lot_of_data_is_scavenged_with_ingore_harddelete_log_v3<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override bool UnsafeIgnoreHardDelete() {
			return true;
		}

		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "bla"),
					Rec.Prepare(0, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Prepare(1, "bla"),
					Rec.Delete(2, "bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			var keep = new int[] { 0 }; // "bla" created

			return new[] {
				dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray(),
			};
		}

		[Test]
		public void only_delete_tombstone_records_with_their_commits_are_kept() {
			CheckRecords();
		}
	}
}
