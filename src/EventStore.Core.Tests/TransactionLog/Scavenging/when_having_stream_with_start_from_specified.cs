using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_having_stream_with_truncatebefore_specified<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare("$$bla", streamMetadata: new StreamMetadata(truncateBefore: 7)),
					Rec.Prepare("bla"), // event 0
					Rec.Prepare("bla"), // event 1
					Rec.Prepare("bla"), // event 2
					Rec.Prepare("bla"), // event 3
					Rec.Prepare("bla"), // event 4
					Rec.Prepare("bla"), // event 5
					Rec.Prepare("bla"), // event 6
					Rec.Prepare("bla"), // event 7
					Rec.Prepare("bla"), // event 8
					Rec.Prepare("bla")) // event 9
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => new[] {0, 8, 9, 10}.Contains(i)).ToArray()
			};
		}

		[Test]
		public void expired_prepares_are_scavenged() {
			CheckRecords();
		}
	}
}
