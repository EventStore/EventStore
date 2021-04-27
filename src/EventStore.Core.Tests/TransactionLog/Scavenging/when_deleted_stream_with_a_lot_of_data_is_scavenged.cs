using System.Linq;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_deleted_stream_with_a_lot_of_data_is_scavenged<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Delete("bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => new[] {6}.Contains(i)).ToArray(),
			};
		}

		[Test]
		public void only_delete_tombstone_records_with_their_commits_are_kept() {
			CheckRecords();
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_deleted_stream_with_a_lot_of_data_is_scavenged_with_ingore_harddelete<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override bool UnsafeIgnoreHardDelete() {
			return true;
		}

		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Delete("bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
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
