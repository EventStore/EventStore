using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture]
	public class when_scavenging_tfchunk_with_version0_log_records_and_incomplete_chunk : ScavengeTestScenario {
		private const byte _version = LogRecordVersion.LogRecordV0;

		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator.Chunk(Rec.Prepare(0, "ES1", version: _version),
					Rec.Commit(0, "ES1", version: _version),
					Rec.Prepare(1, "ES1", version: _version),
					Rec.Commit(1, "ES1", version: _version))
				.CompleteLastChunk()
				.Chunk(Rec.Prepare(2, "ES2", version: _version),
					Rec.Commit(2, "ES2", version: _version),
					Rec.Prepare(3, "ES2", version: _version),
					Rec.Commit(3, "ES2", version: _version))
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				new[] {
					dbResult.Recs[0][0],
					dbResult.Recs[0][1],
					dbResult.Recs[0][2],
					dbResult.Recs[0][3]
				},
				new[] {
					dbResult.Recs[1][0],
					dbResult.Recs[1][1],
					dbResult.Recs[1][2],
					dbResult.Recs[1][3]
				}
			};
		}

		[Test]
		public void should_not_have_changed_any_records() {
			CheckRecords();
		}
	}
}
