using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class
		when_stream_is_softdeleted_and_temp_and_all_events_and_metaevents_are_in_one_chunk<TLogFormat, TStreamId> : ScavengeTestScenario<TLogFormat, TStreamId> {
		protected override DbResult CreateDb(TFChunkDbCreationHelper<TLogFormat, TStreamId> dbCreator) {
			return dbCreator.Chunk(Rec.Prepare("$$test", streamMetadata: new StreamMetadata(tempStream: true)),
					Rec.Prepare("test"),
					Rec.Prepare("test"),
					Rec.Prepare("$$test",
						streamMetadata: new StreamMetadata(truncateBefore: EventNumber.DeletedStream, tempStream: true)))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override ILogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {new ILogRecord[0]};
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
		public void the_metastream_is_absent_logically() {
			Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent("$$test", 0).Result);
			Assert.AreEqual(ReadStreamResult.Success, ReadIndex.ReadStreamEventsForward("$$test", 0, 100).Result);
			Assert.AreEqual(ReadStreamResult.Success, ReadIndex.ReadStreamEventsBackward("$$test", -1, 100).Result);
		}

		[Test]
		public void the_stream_is_absent_physically() {
			var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
			Assert.IsEmpty(ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records
				.Where(x => x.Event.EventStreamId == "test"));
			Assert.IsEmpty(ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records
				.Where(x => x.Event.EventStreamId == "test"));
		}

		[Test]
		public void the_metastream_is_absent_physically() {
			var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
			Assert.IsEmpty(ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records
				.Where(x => x.Event.EventStreamId == "$$test"));
			Assert.IsEmpty(ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records
				.Where(x => x.Event.EventStreamId == "$$test"));
		}
	}
}
