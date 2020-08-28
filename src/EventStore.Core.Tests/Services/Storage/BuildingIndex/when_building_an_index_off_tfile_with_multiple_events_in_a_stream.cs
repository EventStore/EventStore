using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.BuildingIndex {
	[TestFixture]
	public class when_building_an_index_off_tfile_with_multiple_events_in_a_stream : ReadIndexTestScenario {
		private Guid _id1;
		private Guid _id2;

		protected override void WriteTestScenario() {
			_id1 = Guid.NewGuid();
			_id2 = Guid.NewGuid();
			long pos1, pos2, pos3, pos4;
			Writer.Write(new PrepareLogRecord(0, _id1, _id1, 0, 0, "test1", ExpectedVersion.NoStream, DateTime.UtcNow,
					PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
				out pos1);
			Writer.Write(new PrepareLogRecord(pos1, _id2, _id2, pos1, 0, "test1", 0, DateTime.UtcNow,
					PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
				out pos2);
			Writer.Write(new CommitLogRecord(pos2, _id1, 0, DateTime.UtcNow, 0), out pos3);
			Writer.Write(new CommitLogRecord(pos3, _id2, pos1, DateTime.UtcNow, 1), out pos4);
		}

		[Test]
		public void no_event_is_returned_when_nonexistent_stream_is_requested() {
			var result = ReadIndex.ReadEvent("test2", 0);
			Assert.AreEqual(ReadEventResult.NoStream, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void the_first_event_can_be_read() {
			var result = ReadIndex.ReadEvent("test1", 0);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_id1, result.Record.EventId);
		}

		[Test]
		public void the_second_event_can_be_read() {
			var result = ReadIndex.ReadEvent("test1", 1);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_id2, result.Record.EventId);
		}

		[Test]
		public void the_third_event_is_not_found() {
			var result = ReadIndex.ReadEvent("test1", 2);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void the_last_event_is_returned() {
			var result = ReadIndex.ReadEvent("test1", -1);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_id2, result.Record.EventId);
		}

		[Test]
		public void the_stream_can_be_read_with_two_events_in_right_order_when_starting_from_specified_event_number() {
			var result = ReadIndex.ReadStreamEventsBackward("test1", 1, 10);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);

			Assert.AreEqual(_id1, result.Records[1].EventId);
			Assert.AreEqual(_id2, result.Records[0].EventId);
		}

		[Test]
		public void the_stream_can_be_read_with_two_events_backward_from_end() {
			var result = ReadIndex.ReadStreamEventsBackward("test1", -1, 10);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);

			Assert.AreEqual(_id1, result.Records[1].EventId);
			Assert.AreEqual(_id2, result.Records[0].EventId);
		}

		[Test]
		public void the_stream_returns_events_with_correct_pagination() {
			var result = ReadIndex.ReadStreamEventsBackward("test1", 0, 10);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);

			Assert.AreEqual(_id1, result.Records[0].EventId);
		}

		[Test]
		public void the_stream_returns_nothing_for_nonexistent_page() {
			var result = ReadIndex.ReadStreamEventsBackward("test1", 100, 10);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(0, result.Records.Length);
		}

		[Test]
		public void no_events_are_return_if_event_stream_doesnt_exist() {
			var result = ReadIndex.ReadStreamEventsBackward("test2", 0, 10);
			Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
			Assert.IsNotNull(result.Records);
			Assert.IsEmpty(result.Records);
		}

		[Test]
		public void read_all_events_forward_returns_all_events_in_correct_order() {
			var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 10).Records;

			Assert.AreEqual(2, records.Count);
			Assert.AreEqual(_id1, records[0].Event.EventId);
			Assert.AreEqual(_id2, records[1].Event.EventId);
		}

		[Test]
		public void read_all_events_backward_returns_all_events_in_correct_order() {
			var records = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 10).Records;

			Assert.AreEqual(2, records.Count);
			Assert.AreEqual(_id1, records[1].Event.EventId);
			Assert.AreEqual(_id2, records[0].Event.EventId);
		}
	}
}
