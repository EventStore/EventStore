using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture(typeof(LogFormat.V2), typeof(string), LogRecordVersion.LogRecordV0)]
	[TestFixture(typeof(LogFormat.V2), typeof(string), LogRecordVersion.LogRecordV1)]
	[TestFixture(typeof(LogFormat.V3), typeof(long), LogRecordVersion.LogRecordV1)]
	public class when_scavenging_tfchunk_with_incomplete_chunk<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		private readonly byte _version = LogRecordVersion.LogRecordV0;

		public when_scavenging_tfchunk_with_incomplete_chunk(byte version) {
			_version = version;
		}

		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
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

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
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
