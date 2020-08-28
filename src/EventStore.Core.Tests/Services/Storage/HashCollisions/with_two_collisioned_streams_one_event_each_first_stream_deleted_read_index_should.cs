using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Data;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.HashCollisions {
	[TestFixture]
	public class
		with_two_collisioned_streams_one_event_each_first_stream_deleted_read_index_should : ReadIndexTestScenario {
		private EventRecord _prepare1;
		private EventRecord _delete1;
		private EventRecord _prepare2;

		protected override void WriteTestScenario() {
			_prepare1 = WriteSingleEvent("AB", 0, "test1");
			_delete1 = WriteDelete("AB");

			_prepare2 = WriteSingleEvent("CD", 0, "test2");
		}

		[Test]
		public void return_correct_last_event_version_for_first_stream() {
			Assert.AreEqual(EventNumber.DeletedStream, ReadIndex.GetStreamLastEventNumber("AB"));
		}

		[Test]
		public void not_find_log_record_for_first_stream() {
			var result = ReadIndex.ReadEvent("AB", 0);
			Assert.AreEqual(ReadEventResult.StreamDeleted, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void return_empty_range_on_from_start_range_query_for_first_stream() {
			var result = ReadIndex.ReadStreamEventsForward("AB", 0, 1);
			Assert.AreEqual(ReadStreamResult.StreamDeleted, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_on_from_end_range_query_for_first_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("AB", 0, 1);
			Assert.AreEqual(ReadStreamResult.StreamDeleted, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_on_from_start_range_query_for_invalid_arguments_for_first_stream() {
			var result = ReadIndex.ReadStreamEventsForward("AB", 1, 1);
			Assert.AreEqual(ReadStreamResult.StreamDeleted, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_on_from_end_range_query_for_invalid_arguments_for_first_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("AB", 1, 1);
			Assert.AreEqual(ReadStreamResult.StreamDeleted, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_correct_last_event_version_for_second_stream() {
			Assert.AreEqual(0, ReadIndex.GetStreamLastEventNumber("CD"));
		}

		[Test]
		public void return_correct_log_record_for_second_stream() {
			var result = ReadIndex.ReadEvent("CD", 0);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_prepare2, result.Record);
		}

		[Test]
		public void return_correct_range_on_from_start_range_query_for_second_stream() {
			var result = ReadIndex.ReadStreamEventsForward("CD", 0, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(_prepare2, result.Records[0]);
		}

		[Test]
		public void return_correct_range_on_from_end_range_query_for_second_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("CD", 0, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
			Assert.AreEqual(_prepare2, result.Records[0]);
		}

		[Test]
		public void return_correct_last_event_version_for_nonexistent_stream_with_same_hash() {
			Assert.AreEqual(-1, ReadIndex.GetStreamLastEventNumber("EF"));
		}

		[Test]
		public void not_find_log_record_for_nonexistent_stream_with_same_hash() {
			var result = ReadIndex.ReadEvent("EF", 0);
			Assert.AreEqual(ReadEventResult.NoStream, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void not_return_range_for_non_existing_stream_with_same_hash() {
			var result = ReadIndex.ReadStreamEventsBackward("EF", 0, 1);
			Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_all_events_on_read_all_forward() {
			var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(3, events.Length);
			Assert.AreEqual(_prepare1, events[0]);
			Assert.AreEqual(_delete1, events[1]);
			Assert.AreEqual(_prepare2, events[2]);
		}

		[Test]
		public void return_all_events_on_read_all_backward() {
			var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event)
				.ToArray();
			Assert.AreEqual(3, events.Length);
			Assert.AreEqual(_prepare1, events[2]);
			Assert.AreEqual(_delete1, events[1]);
			Assert.AreEqual(_prepare2, events[0]);
		}
	}
}
