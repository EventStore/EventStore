using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture]
	public class
		when_stream_is_softdeleted_and_temp_with_log_version_0_but_some_events_are_in_multiple_chunks :
			ScavengeTestScenario {
		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			var version = LogRecordVersion.LogRecordV0;
			return dbCreator.Chunk(Rec.Prepare(0, "test", version: version),
					Rec.Commit(0, "test", version: version))
				.Chunk(Rec.Prepare(1, "test", version: version),
					Rec.Commit(1, "test", version: version),
					Rec.Prepare(2, "$$test", metadata: new StreamMetadata(null, null, int.MaxValue, true, null, null),
						version: version),
					Rec.Commit(2, "$$test", version: version))
				.Chunk(
					Rec.Prepare(3, "random",
						version: version), // Need an incomplete chunk to ensure writer checkpoints are correct
					Rec.Commit(3, "random", version: version))
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				new LogRecord[0],
				new[] {
					dbResult.Recs[1][0],
					dbResult.Recs[1][1],
					dbResult.Recs[1][2],
					dbResult.Recs[1][3]
				},
				new[] {
					dbResult.Recs[2][0],
					dbResult.Recs[2][1]
				}
			};
		}

		[Test]
		public void scavenging_goes_as_expected() {
			CheckRecords();
		}

		[Test]
		public void the_stream_is_absent_logically() {
			Assert.AreEqual(ReadStreamResult.NoStream, ReadIndex.ReadStreamEventsForward("test", 0, 100).Result,
				"Read test stream forward");
			Assert.AreEqual(ReadStreamResult.NoStream, ReadIndex.ReadStreamEventsBackward("test", -1, 100).Result,
				"Read test stream backward");
			Assert.AreEqual(ReadEventResult.NoStream, ReadIndex.ReadEvent("test", 0).Result,
				"Read single event from test stream");
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
					.Count(x => x.Event.EventStreamId == "$$test"), "Read $$test stream forward");
			Assert.AreEqual(1,
				ReadIndex.ReadAllEventsBackward(headOfTf, 10).Records.Count(x => x.Event.EventStreamId == "$$test"),
				"Read $$test stream backward");
		}
	}
}
