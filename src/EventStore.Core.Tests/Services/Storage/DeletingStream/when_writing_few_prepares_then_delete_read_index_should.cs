using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_writing_few_prepares_then_delete_read_index_should<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private EventRecord _event0;
		private EventRecord _event1;

		protected override void WriteTestScenario() {
			_event0 = WriteSingleEvent("ES", 0, "bla1");
			Assert.True(_logFormat.StreamNameIndex.GetOrReserve("ES", out var esStreamId, out _, out _));
			_event1 = WriteSingleEvent("ES", 1, "bla1");
			var prepare = LogRecord.DeleteTombstone(_recordFactory, WriterCheckpoint.ReadNonFlushed(), Guid.NewGuid(), Guid.NewGuid(),
				esStreamId, EventNumber.DeletedStream - 1, PrepareFlags.IsCommitted);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
		}

		[Test]
		public void indicate_that_stream_is_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("ES"), Is.True);
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
		public void read_single_events_should_return_stream_deleted() {
			var result = ReadIndex.ReadEvent("ES", 0);
			Assert.AreEqual(ReadEventResult.StreamDeleted, result.Result);
			Assert.IsNull(result.Record);

			result = ReadIndex.ReadEvent("ES", 1);
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
		public void read_all_forward_should_return_all_stream_records_including_delete_record() {
			var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).EventRecords()
				.Select(r => r.Event)
				.ToArray();
			Assert.AreEqual(3, events.Length);
			Assert.AreEqual(_event0, events[0]);
			Assert.AreEqual(_event1, events[1]);
			Assert.AreEqual(EventNumber.DeletedStream - 1, events[2].ExpectedVersion);
		}

		[Test]
		public void read_all_backward_should_return_all_stream_records_including_delete_record() {
			var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).EventRecords()
				.Select(r => r.Event)
				.ToArray();
			Assert.AreEqual(3, events.Length);
			Assert.AreEqual(EventNumber.DeletedStream - 1, events[0].ExpectedVersion);
			Assert.AreEqual(_event1, events[1]);
			Assert.AreEqual(_event0, events[2]);
		}
	}
}
