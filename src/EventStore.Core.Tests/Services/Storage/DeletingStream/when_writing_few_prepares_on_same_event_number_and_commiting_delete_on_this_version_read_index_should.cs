using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class
		when_writing_few_prepares_with_same_event_number_and_commiting_delete_on_this_version_read_index_should<TLogFormat, TStreamId> :
			ReadIndexTestScenario<TLogFormat, TStreamId> {
		private EventRecord _deleteTombstone;

		protected override void WriteTestScenario() {
			long pos;
			string stream = "ES";
			_streamNameIndex.GetOrAddId(stream, out var streamId);

			var prepare1 = LogRecord.SingleWrite(_recordFactory, WriterCheckpoint.ReadNonFlushed(), // prepare1
				Guid.NewGuid(),
				Guid.NewGuid(),
				streamId,
				-1,
				"some-type",
				LogRecord.NoData,
				null,
				DateTime.UtcNow);
			Assert.IsTrue(Writer.Write(prepare1, out pos));

			var prepare2 = LogRecord.SingleWrite(_recordFactory, WriterCheckpoint.ReadNonFlushed(), // prepare2
				Guid.NewGuid(),
				Guid.NewGuid(),
				streamId,
				-1,
				"some-type",
				LogRecord.NoData,
				null,
				DateTime.UtcNow);
			Assert.IsTrue(Writer.Write(prepare2, out pos));


			var deletePrepare = LogRecord.DeleteTombstone(_recordFactory, WriterCheckpoint.ReadNonFlushed(), // delete prepare
				Guid.NewGuid(), Guid.NewGuid(), streamId, -1);
			_deleteTombstone = new EventRecord(EventNumber.DeletedStream, deletePrepare, stream);
			Assert.IsTrue(Writer.Write(deletePrepare, out pos));

			var prepare3 = LogRecord.SingleWrite(_recordFactory, WriterCheckpoint.ReadNonFlushed(), // prepare3
				Guid.NewGuid(),
				Guid.NewGuid(),
				streamId,
				-1,
				"some-type",
				LogRecord.NoData,
				null,
				DateTime.UtcNow);
			Assert.IsTrue(Writer.Write(prepare3, out pos));

			var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), // committing delete
				deletePrepare.CorrelationId,
				deletePrepare.LogPosition,
				EventNumber.DeletedStream);
			Assert.IsTrue(Writer.Write(commit, out pos));
		}

		[Test]
		public void indicate_that_stream_is_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("ES"));
		}

		[Test]
		public void indicate_that_nonexisting_stream_with_same_hash_is_not_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("ZZ"), Is.False);
		}

		[Test]
		public void indicate_that_nonexisting_stream_with_different_hash_is_not_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
		}

		[Test]
		public void read_single_events_with_number_0_should_return_stream_deleted() {
			var result = ReadIndex.ReadEvent("ES", 0);
			Assert.AreEqual(ReadEventResult.StreamDeleted, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void read_single_events_with_number_1_should_return_stream_deleted() {
			var result = ReadIndex.ReadEvent("ES", 1);
			Assert.AreEqual(ReadEventResult.StreamDeleted, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void read_stream_events_forward_should_return_stream_deleted() {
			var result = ReadIndex.ReadStreamEventsForward("ES", 0, 100);
			Assert.AreEqual(ReadStreamResult.StreamDeleted, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void read_stream_events_backward_should_return_stream_deleted() {
			var result = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
			Assert.AreEqual(ReadStreamResult.StreamDeleted, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void read_all_forward_should_return_all_stream_records_except_uncommited() {
			var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(1, events.Length);
			Assert.AreEqual(_deleteTombstone, events[0]);
		}

		[Test]
		public void read_all_backward_should_return_all_stream_records_except_uncommited() {
			var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event)
				.ToArray();
			Assert.AreEqual(1, events.Length);
			Assert.AreEqual(_deleteTombstone, events[0]);
		}
	}
}
