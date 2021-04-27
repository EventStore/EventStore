using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_metastream_is_scavenged_and_read_index_is_set_to_keep_last_3_metaevents<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		public when_metastream_is_scavenged_and_read_index_is_set_to_keep_last_3_metaevents() :
			base(metastreamMaxCount: 3) {
		}

		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator
				.Chunk(Rec.Prepare("$$bla"),
					Rec.Prepare("$$bla"),
					Rec.Prepare("$$bla"),
					Rec.Prepare("$$bla"),
					Rec.Prepare("$$bla"))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				dbResult.Recs[0].Where((x, i) => i >= 2).ToArray()
			};
		}

		[Test]
		public void metastream_is_scavenged_correctly() {
			CheckRecords();
		}
	}
}
