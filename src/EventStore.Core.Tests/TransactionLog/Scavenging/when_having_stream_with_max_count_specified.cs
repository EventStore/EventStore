using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_having_stream_with_max_count_specified<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare("$$bla", streamMetadata: new StreamMetadata(maxCount: 3)),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"),
					Rec.Prepare("bla"))
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
