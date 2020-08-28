using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Data;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.HashCollisions {
	[TestFixture]
	public class with_three_collisioned_streams_one_event_each_read_index_should : ReadIndexTestScenario {
		private EventRecord _prepare1;
		private EventRecord _prepare2;
		private EventRecord _prepare3;

		protected override void WriteTestScenario() {
			_prepare1 = WriteSingleEvent("AB", 0, "test1");
			_prepare2 = WriteSingleEvent("CD", 0, "test2");
			_prepare3 = WriteSingleEvent("EF", 0, "test3");
		}

		[Test]
		public void return_correct_last_event_version_for_first_stream() {
			Assert.AreEqual(0, ReadIndex.GetStreamLastEventNumber("AB"));
		}

		[Test]
		public void return_correct_log_record_for_first_stream() {
			var result = ReadIndex.ReadEvent("AB", 0);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_prepare1, result.Record);
		}

		[Test]
		public void not_find_record_with_index_1_in_first_stream() {
			var result = ReadIndex.ReadEvent("AB", 1);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void return_correct_range_on_from_start_range_query_for_first_stream() {
			var result = ReadIndex.ReadStreamEventsForward("AB", 0, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
			Assert.AreEqual(_prepare1, result.Records[0]);
		}

		[Test]
		public void return_correct_range_on_from_end_range_query_for_first_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("AB", 0, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
			Assert.AreEqual(_prepare1, result.Records[0]);
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
		public void not_find_record_with_index_1_in_second_stream() {
			var result = ReadIndex.ReadEvent("CD", 1);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void return_correct_range_on_from_start_range_query_for_second_stream() {
			var result = ReadIndex.ReadStreamEventsForward("CD", 0, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
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
		public void return_correct_last_event_version_for_third_stream() {
			Assert.AreEqual(0, ReadIndex.GetStreamLastEventNumber("EF"));
		}

		[Test]
		public void return_correct_log_record_for_third_stream() {
			var result = ReadIndex.ReadEvent("EF", 0);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_prepare3, result.Record);
		}

		[Test]
		public void not_find_record_with_index_1_in_third_stream() {
			var result = ReadIndex.ReadEvent("EF", 1);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void return_correct_range_on_from_start_range_query_for_third_stream() {
			var result = ReadIndex.ReadStreamEventsForward("EF", 0, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
			Assert.AreEqual(_prepare3, result.Records[0]);
		}

		[Test]
		public void return_correct_range_on_from_end_range_query_for_third_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("EF", 0, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
			Assert.AreEqual(_prepare3, result.Records[0]);
		}

		[Test]
		public void return_empty_range_when_asked_to_get_few_events_from_start_starting_from_1_in_first_stream() {
			var result = ReadIndex.ReadStreamEventsForward("AB", 1, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_when_asked_to_get_few_events_from_start_starting_from_1_in_second_stream() {
			var result = ReadIndex.ReadStreamEventsForward("CD", 1, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_when_asked_to_get_few_events_from_start_starting_from_1_in_third_stream() {
			var result = ReadIndex.ReadStreamEventsForward("EF", 1, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_when_asked_to_get_few_events_from_end_starting_from_1_in_first_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("AB", 1, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_when_asked_to_get_few_events_from_end_starting_from_1_in_second_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("CD", 1, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_when_asked_to_get_few_events_from_end_starting_from_1_in_third_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("EF", 1, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_correct_last_event_version_for_nonexistent_stream_with_same_hash() {
			Assert.AreEqual(-1, ReadIndex.GetStreamLastEventNumber("ZZ"));
		}

		[Test]
		public void not_find_log_record_for_nonexistent_stream_with_same_hash() {
			var result = ReadIndex.ReadEvent("ZZ", 0);
			Assert.AreEqual(ReadEventResult.NoStream, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void return_empty_range_on_from_start_range_query_for_non_existing_stream_with_same_hash() {
			var result = ReadIndex.ReadStreamEventsForward("ZZ", 0, 1);
			Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void return_empty_range_on_from_end_range_query_for_non_existing_stream_with_same_hash() {
			var result = ReadIndex.ReadStreamEventsBackward("ZZ", 0, 1);
			Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}
	}
}
