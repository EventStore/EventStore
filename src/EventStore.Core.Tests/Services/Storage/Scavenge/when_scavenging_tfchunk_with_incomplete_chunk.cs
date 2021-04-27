using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.LogCommon;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_scavenging_tfchunk_with_incomplete_chunk<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		private const byte _version = LogRecordVersion.LogRecordV0;

		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator.Chunk(Rec.Prepare("ES1"),
				Rec.Prepare("ES1"))
				.CompleteLastChunk()
				.Chunk(Rec.Prepare("ES2"),
				Rec.Prepare("ES2"))
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				new[] {
					dbResult.Recs[0][0],
					dbResult.Recs[0][1]
				},
				new[] {
					dbResult.Recs[1][0],
					dbResult.Recs[1][1]
				}
			};
		}

		[Test]
		public void should_not_have_changed_any_records() {
			CheckRecords();
		}
	}
}
