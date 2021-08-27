using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_having_stream_with_truncatebefore_specified_log_v3<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare(0, "$$bla", metadata: new StreamMetadata(truncateBefore: 7)),
					Rec.Prepare(1, "bla"), // event 0
					Rec.Prepare(2, "bla"), // event 1
					Rec.Prepare(2, "bla"), // event 2
					Rec.Prepare(2, "bla"), // event 3
					Rec.Prepare(2, "bla"), // event 4
					Rec.Prepare(2, "bla"), // event 5
					Rec.Prepare(3, "bla"), // event 6
					Rec.Prepare(3, "bla"), // event 7
					Rec.Prepare(3, "bla"), // event 8
					Rec.Prepare(3, "bla")) // event 9
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			var keep = new int[] { 0, 1, 9, 10, 11 };

			return new[] {
				dbResult.Recs[0].Where((x, i) => keep.Contains(i)).ToArray(),
			};
		}

		[Test]
		public void expired_prepares_are_scavenged() {
			CheckRecords();
		}
	}
}
