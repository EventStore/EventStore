using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class
		when_having_stream_with_both_max_age_and_max_count_with_stricter_max_age_specified<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(
					Rec.Prepare("$$bla", streamMetadata: new StreamMetadata(5, TimeSpan.FromMinutes(5), null, null, null)),
					Rec.Prepare("bla"),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(100)),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(90)),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(60)),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(40)),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(30)),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(20)),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(3)),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(2)),
					Rec.Prepare("bla", timestamp: DateTime.UtcNow - TimeSpan.FromMinutes(1)))
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
