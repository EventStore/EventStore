using System.Linq;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long), Ignore = "Explicit transactions are not supported yet by Log V3")]
	public class when_stream_is_deleted_and_bulk_transaction_spans_chunks_boundary<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.TransPrepare(0, "bla"),
					Rec.TransCommit(0, "bla"),
					Rec.TransPrepare(1, "bla"),
					Rec.TransPrepare(1, "bla"),
					Rec.TransPrepare(1, "bla"),
					Rec.TransPrepare(1, "bla"))
				.Chunk(Rec.TransPrepare(1, "bla"),
					Rec.TransPrepare(1, "bla"),
					Rec.TransPrepare(1, "bla"),
					Rec.TransCommit(1, "bla"),
					Rec.Delete("bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				new[] {dbResult.Recs[0][2]}, // first prepare in commit that is in different chunk
				dbResult.Recs[1].Where((x, i) => i >= 3).ToArray(),
			};
		}

		[Test]
		public void first_prepare_of_transaction_is_preserved() {
			CheckRecords();
		}
	}
}
