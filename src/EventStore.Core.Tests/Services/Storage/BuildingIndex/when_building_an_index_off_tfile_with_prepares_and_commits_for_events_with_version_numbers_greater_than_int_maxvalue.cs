using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.BuildingIndex {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class
		when_building_an_index_off_tfile_with_prepares_and_commits_for_events_with_version_numbers_greater_than_int_maxvalue<TLogFormat, TStreamId>
		: ReadIndexTestScenario<TLogFormat, TStreamId> {
		private Guid _id1;
		private Guid _id2;
		private Guid _id3;

		private long firstEventNumber = (long)int.MaxValue + 1;
		private long secondEventNumber = (long)int.MaxValue + 2;
		private long thirdEventNumber = (long)int.MaxValue + 3;

		protected override void WriteTestScenario() {
			_id1 = Guid.NewGuid();
			_id2 = Guid.NewGuid();
			_id3 = Guid.NewGuid();
			long pos0, pos1, pos2, pos3, pos4, pos5, pos6;

			GetOrReserve("test1", out var test1StreamId, out _);
			GetOrReserve("test2", out var test2StreamId, out pos0);

			Writer.Write(LogRecord.SingleWrite(_recordFactory, pos0, _id1, _id1, test1StreamId, firstEventNumber, "type", new byte[0],
					new byte[0], DateTime.UtcNow), out pos1);
			Writer.Write(LogRecord.SingleWrite(_recordFactory, pos1, _id2, _id2, test2StreamId, secondEventNumber, "type", new byte[0],
					new byte[0], DateTime.UtcNow), out pos2);
			Writer.Write(LogRecord.SingleWrite(_recordFactory, pos2, _id3, _id3, test2StreamId, thirdEventNumber, "type", new byte[0],
					new byte[0], DateTime.UtcNow), out pos3);
			Writer.Write(new CommitLogRecord(pos3, _id1, pos0, DateTime.UtcNow, firstEventNumber), out pos4);
			Writer.Write(new CommitLogRecord(pos4, _id2, pos1, DateTime.UtcNow, secondEventNumber), out pos5);
			Writer.Write(new CommitLogRecord(pos5, _id3, pos2, DateTime.UtcNow, thirdEventNumber), out pos6);
		}

		[Test]
		public void the_first_event_can_be_read() {
			var result = ReadIndex.ReadEvent("test1", firstEventNumber);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_id1, result.Record.EventId);
		}

		[Test]
		public void the_nonexisting_event_can_not_be_read() {
			var result = ReadIndex.ReadEvent("test1", firstEventNumber + 1);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void the_second_event_can_be_read() {
			var result = ReadIndex.ReadEvent("test2", secondEventNumber);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_id2, result.Record.EventId);
		}

		[Test]
		public void the_last_event_of_first_stream_can_be_read() {
			var result = ReadIndex.ReadEvent("test1", -1);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_id1, result.Record.EventId);
		}

		[Test]
		public void the_last_event_of_second_stream_can_be_read() {
			var result = ReadIndex.ReadEvent("test2", -1);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_id3, result.Record.EventId);
		}

		[Test]
		public void the_stream_can_be_read_for_first_stream() {
			var result = ReadIndex.ReadStreamEventsBackward("test1", firstEventNumber, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
			Assert.AreEqual(_id1, result.Records[0].EventId);
		}

		[Test]
		public void the_stream_can_be_read_for_second_stream_from_end() {
			var result = ReadIndex.ReadStreamEventsBackward("test2", -1, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
			Assert.AreEqual(_id3, result.Records[0].EventId);
		}

		[Test]
		public void the_stream_can_be_read_for_second_stream_from_event_number() {
			var result = ReadIndex.ReadStreamEventsBackward("test2", thirdEventNumber, 1);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(1, result.Records.Length);
			Assert.AreEqual(_id3, result.Records[0].EventId);
		}

		[Test]
		public void read_all_events_forward_returns_all_events_in_correct_order() {
			var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 10).EventRecords();

			Assert.AreEqual(3, records.Count);
			Assert.AreEqual(_id1, records[0].Event.EventId);
			Assert.AreEqual(_id2, records[1].Event.EventId);
			Assert.AreEqual(_id3, records[2].Event.EventId);
		}

		[Test]
		public void read_all_events_backward_returns_all_events_in_correct_order() {
			var records = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 10).EventRecords();

			Assert.AreEqual(3, records.Count);
			Assert.AreEqual(_id1, records[2].Event.EventId);
			Assert.AreEqual(_id2, records[1].Event.EventId);
			Assert.AreEqual(_id3, records[0].Event.EventId);
		}
	}
}
