using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_stream_is_softdeleted_and_temp_but_some_metaevents_are_in_multiple_chunks<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator.Chunk(Rec.Prepare("$$test", streamMetadata: new StreamMetadata(tempStream: true)))
				.Chunk(Rec.Prepare("test"),
					Rec.Prepare("test"),
					Rec.Prepare("$$test",
						streamMetadata: new StreamMetadata(truncateBefore: EventNumber.DeletedStream, tempStream: true)))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				new ILogRecord[0],
				new[] {
					dbResult.Recs[1][1],
					dbResult.Recs[1][2]
				}
			};
		}

		[Test]
		public void scavenging_goes_as_expected() {
			CheckRecords();
		}

		[Test]
		public void the_stream_is_absent_logically() {
			Assert.AreEqual(ReadEventResult.NoStream, ReadIndex.ReadEvent("test", 0).Result);
			Assert.AreEqual(ReadStreamResult.NoStream, ReadIndex.ReadStreamEventsForward("test", 0, 100).Result);
			Assert.AreEqual(ReadStreamResult.NoStream, ReadIndex.ReadStreamEventsBackward("test", -1, 100).Result);
		}

		[Test]
		public void the_metastream_is_present_logically() {
			Assert.AreEqual(ReadEventResult.Success, ReadIndex.ReadEvent("$$test", -1).Result);
			Assert.AreEqual(ReadStreamResult.Success, ReadIndex.ReadStreamEventsForward("$$test", 0, 100).Result);
			Assert.AreEqual(1, ReadIndex.ReadStreamEventsForward("$$test", 0, 100).Records.Length);
			Assert.AreEqual(ReadStreamResult.Success, ReadIndex.ReadStreamEventsBackward("$$test", -1, 100).Result);
			Assert.AreEqual(1, ReadIndex.ReadStreamEventsBackward("$$test", -1, 100).Records.Length);
		}

		[Test]
		public void the_stream_is_present_physically() {
			var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
			Assert.AreEqual(1,
				ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records
					.Count(x => x.Event.EventStreamId == "test"));
			Assert.AreEqual(1,
				ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records.Count(x => x.Event.EventStreamId == "test"));
		}

		[Test]
		public void the_metastream_is_present_physically() {
			var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
			Assert.AreEqual(1,
				ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records
					.Count(x => x.Event.EventStreamId == "$$test"));
			Assert.AreEqual(1,
				ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records.Count(x => x.Event.EventStreamId == "$$test"));
		}
	}
}
