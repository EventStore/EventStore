using System;
using EventStore.Core.Data;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount.ReadRangeAndNextEventNumber {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_reading_stream_with_max_age_and_max_count_and_max_count_is_more_strict<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private EventRecord _event2;
		private EventRecord _event3;
		private EventRecord _event4;

		protected override void WriteTestScenario() {
			var now = DateTime.UtcNow;

			var metadata = string.Format(@"{{""$maxAge"":{0},""$maxCount"":3}}",
				(int)TimeSpan.FromMinutes(61).TotalSeconds);
			WriteStreamMetadata("ES", 0, metadata, now.AddMinutes(-100));

			WriteSingleEvent("ES", 0, "bla", now.AddMinutes(-50));
			WriteSingleEvent("ES", 1, "bla", now.AddMinutes(-25));
			_event2 = WriteSingleEvent("ES", 2, "bla", now.AddMinutes(-15));
			_event3 = WriteSingleEvent("ES", 3, "bla", now.AddMinutes(-11));
			_event4 = WriteSingleEvent("ES", 4, "bla", now.AddMinutes(-3));
		}

		[Test]
		public void
			on_read_forward_from_start_to_expired_next_event_number_is_first_active_and_its_not_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsForward("ES", 0, 2);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(2, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsFalse(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(0, records.Length);
		}

		[Test]
		public void
			on_read_forward_from_start_to_active_next_event_number_is_last_read_event_plus_1_and_its_not_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsForward("ES", 0, 4);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(4, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsFalse(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(2, records.Length);
			Assert.AreEqual(_event2, records[0]);
			Assert.AreEqual(_event3, records[1]);
		}

		[Test]
		public void
			on_read_forward_from_expired_to_active_next_event_number_is_last_read_event_plus_1_and_its_not_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsForward("ES", 1, 2);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(3, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsFalse(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event2, records[0]);
		}

		[Test]
		public void on_read_forward_from_expired_to_end_next_event_number_is_end_plus_1_and_its_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsForward("ES", 1, 4);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(5, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsTrue(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(3, records.Length);
			Assert.AreEqual(_event2, records[0]);
			Assert.AreEqual(_event3, records[1]);
			Assert.AreEqual(_event4, records[2]);
		}

		[Test]
		public void
			on_read_forward_from_expired_to_out_of_bounds_next_event_number_is_end_plus_1_and_its_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsForward("ES", 1, 6);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(5, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsTrue(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(3, records.Length);
			Assert.AreEqual(_event2, records[0]);
			Assert.AreEqual(_event3, records[1]);
			Assert.AreEqual(_event4, records[2]);
		}

		[Test]
		public void
			on_read_forward_from_out_of_bounds_to_out_of_bounds_next_event_number_is_end_plus_1_and_its_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsForward("ES", 7, 2);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(5, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsTrue(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(0, records.Length);
		}


		[Test]
		public void
			on_read_backward_from_end_to_active_next_event_number_is_last_read_event_minus_1_and_its_not_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsBackward("ES", 4, 2);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(2, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsFalse(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(2, records.Length);
			Assert.AreEqual(_event4, records[0]);
			Assert.AreEqual(_event3, records[1]);
		}

		[Test]
		public void on_read_backward_from_end_to_maxcount_bound_its_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsBackward("ES", 4, 3);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(-1, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsTrue(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(3, records.Length);
			Assert.AreEqual(_event4, records[0]);
			Assert.AreEqual(_event3, records[1]);
			Assert.AreEqual(_event2, records[2]);
		}

		[Test]
		public void on_read_backward_from_active_to_expired_its_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsBackward("ES", 3, 3);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(-1, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsTrue(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(2, records.Length);
			Assert.AreEqual(_event3, records[0]);
			Assert.AreEqual(_event2, records[1]);
		}

		[Test]
		public void on_read_backward_from_expired_to_expired_its_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsBackward("ES", 1, 2);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(-1, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsTrue(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(0, records.Length);
		}

		[Test]
		public void on_read_backward_from_expired_to_before_start_its_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsBackward("ES", 1, 5);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(-1, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsTrue(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(0, records.Length);
		}

		[Test]
		public void
			on_read_backward_from_out_of_bounds_to_out_of_bounds_next_event_number_is_end_and_its_not_end_of_stream() {
			var res = ReadIndex.ReadStreamEventsBackward("ES", 10, 3);
			Assert.AreEqual(ReadStreamResult.Success, res.Result);
			Assert.AreEqual(4, res.NextEventNumber);
			Assert.AreEqual(4, res.LastEventNumber);
			Assert.IsFalse(res.IsEndOfStream);

			var records = res.Records;
			Assert.AreEqual(0, records.Length);
		}
	}
}
