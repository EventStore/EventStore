using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture]
	public class when_stream_is_softdeleted_with_mixed_log_record_version_0_and_version_1 : ScavengeTestScenario {
		private const string _deletedStream = "test";
		private const string _deletedMetaStream = "$$test";
		private const string _keptStream = "other";

		protected override DbResult CreateDb(TFChunkDbCreationHelper dbCreator) {
			return dbCreator.Chunk(
					Rec.Prepare(0, _deletedMetaStream, metadata: new StreamMetadata(tempStream: true),
						version: LogRecordVersion.LogRecordV0),
					Rec.Commit(0, _deletedMetaStream, version: LogRecordVersion.LogRecordV0),
					Rec.Prepare(1, _keptStream, version: LogRecordVersion.LogRecordV1),
					Rec.Commit(1, _keptStream, version: LogRecordVersion.LogRecordV1),
					Rec.Prepare(2, _keptStream, version: LogRecordVersion.LogRecordV0),
					Rec.Commit(2, _keptStream, version: LogRecordVersion.LogRecordV0),
					Rec.Prepare(3, _deletedStream, version: LogRecordVersion.LogRecordV1),
					Rec.Commit(3, _deletedStream, version: LogRecordVersion.LogRecordV0),
					Rec.Prepare(4, _deletedStream, version: LogRecordVersion.LogRecordV0),
					Rec.Commit(4, _deletedStream, version: LogRecordVersion.LogRecordV1),
					Rec.Prepare(5, _keptStream, version: LogRecordVersion.LogRecordV1),
					Rec.Commit(5, _keptStream, version: LogRecordVersion.LogRecordV1),
					Rec.Prepare(6, _deletedMetaStream,
						metadata: new StreamMetadata(truncateBefore: EventNumber.DeletedStream, tempStream: true),
						version: LogRecordVersion.LogRecordV1),
					Rec.Commit(6, _deletedMetaStream, version: LogRecordVersion.LogRecordV0))
				.CompleteLastChunk()
				.CreateDb();
		}

		protected override LogRecord[][] KeptRecords(DbResult dbResult) {
			return new[] {
				new[] {
					dbResult.Recs[0][2],
					dbResult.Recs[0][3],
					dbResult.Recs[0][4],
					dbResult.Recs[0][5],
					dbResult.Recs[0][10],
					dbResult.Recs[0][11]
				}
			};
		}

		[Test]
		public void scavenging_goes_as_expected() {
			CheckRecords();
		}

		[Test]
		public void the_stream_is_absent_logically() {
			Assert.AreEqual(ReadEventResult.NoStream, ReadIndex.ReadEvent(_deletedStream, 0).Result);
			Assert.AreEqual(ReadStreamResult.NoStream,
				ReadIndex.ReadStreamEventsForward(_deletedStream, 0, 100).Result);
			Assert.AreEqual(ReadStreamResult.NoStream,
				ReadIndex.ReadStreamEventsBackward(_deletedStream, -1, 100).Result);
		}

		[Test]
		public void the_metastream_is_absent_logically() {
			Assert.AreEqual(ReadEventResult.NotFound, ReadIndex.ReadEvent(_deletedMetaStream, 0).Result);
			Assert.AreEqual(ReadStreamResult.Success,
				ReadIndex.ReadStreamEventsForward(_deletedMetaStream, 0, 100).Result);
			Assert.AreEqual(ReadStreamResult.Success,
				ReadIndex.ReadStreamEventsBackward(_deletedMetaStream, -1, 100).Result);
		}

		[Test]
		public void the_stream_is_absent_physically() {
			var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
			Assert.IsEmpty(ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records
				.Where(x => x.Event.EventStreamId == _deletedStream));
			Assert.IsEmpty(ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records
				.Where(x => x.Event.EventStreamId == _deletedStream));
		}

		[Test]
		public void the_metastream_is_absent_physically() {
			var headOfTf = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
			Assert.IsEmpty(ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 1000).Records
				.Where(x => x.Event.EventStreamId == _deletedMetaStream));
			Assert.IsEmpty(ReadIndex.ReadAllEventsBackward(headOfTf, 1000).Records
				.Where(x => x.Event.EventStreamId == _deletedMetaStream));
		}

		[Test]
		public void the_kept_stream_is_present() {
			Assert.AreEqual(ReadEventResult.Success, ReadIndex.ReadEvent(_keptStream, 0).Result);
			Assert.AreEqual(ReadStreamResult.Success, ReadIndex.ReadStreamEventsForward(_keptStream, 0, 100).Result);
			Assert.AreEqual(ReadStreamResult.Success, ReadIndex.ReadStreamEventsBackward(_keptStream, -1, 100).Result);
		}
	}
}
