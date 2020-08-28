using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Data;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture]
	public class when_scavenging_tfchunk_with_version0_log_records_using_transactions : ReadIndexTestScenario {
		private const string _streamIdOne = "ES-1";
		private const string _streamIdTwo = "ES-2";
		private PrepareLogRecord _p1, _p2, _p3, _p4, _p5, _random1;
		private long _t2CommitPos, _t1CommitPos, _postCommitPos;

		protected override void WriteTestScenario() {
			var t1 = WriteTransactionBeginV0(Guid.NewGuid(), WriterCheckpoint.ReadNonFlushed(), _streamIdOne,
				ExpectedVersion.NoStream);
			var t2 = WriteTransactionBeginV0(Guid.NewGuid(), WriterCheckpoint.ReadNonFlushed(), _streamIdTwo,
				ExpectedVersion.NoStream);

			_p1 = WriteTransactionEventV0(t1.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t1.LogPosition, 0,
				t1.EventStreamId, 0, "es1", PrepareFlags.Data);
			_p2 = WriteTransactionEventV0(t2.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t2.LogPosition, 0,
				t2.EventStreamId, 0, "abc1", PrepareFlags.Data);
			_p3 = WriteTransactionEventV0(t1.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t1.LogPosition, 1,
				t1.EventStreamId, 1, "es1", PrepareFlags.Data);
			_p4 = WriteTransactionEventV0(t2.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t2.LogPosition, 1,
				t2.EventStreamId, 1, "abc1", PrepareFlags.Data);
			_p5 = WriteTransactionEventV0(t1.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t1.LogPosition, 2,
				t1.EventStreamId, 2, "es1", PrepareFlags.Data);

			WriteTransactionEndV0(t2.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t2.TransactionPosition,
				t2.EventStreamId);
			WriteTransactionEndV0(t1.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t1.TransactionPosition,
				t1.EventStreamId);

			_t2CommitPos = WriteCommitV0(t2.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t2.TransactionPosition,
				t2.EventStreamId, 0, out _postCommitPos);
			_t1CommitPos = WriteCommitV0(t1.CorrelationId, WriterCheckpoint.ReadNonFlushed(), t1.TransactionPosition,
				t1.EventStreamId, 0, out _postCommitPos);

			Writer.CompleteChunk();

			// Need to have a second chunk as otherwise the checkpoints will be off
			_random1 = WriteSingleEventWithLogVersion0(Guid.NewGuid(), "random-stream",
				WriterCheckpoint.ReadNonFlushed(), 0);

			Scavenge(completeLast: false, mergeChunks: true);
		}

		private PrepareLogRecord WriteTransactionBeginV0(Guid id, long logPosition, string eventStreamId,
			long expectedVersion) {
			var prepare = new PrepareLogRecord(logPosition, id, Guid.NewGuid(), logPosition, -1, eventStreamId,
				expectedVersion,
				DateTime.UtcNow, PrepareFlags.TransactionBegin, null, new byte[0], new byte[0],
				LogRecordVersion.LogRecordV0);
			long pos;
			Assert.IsTrue(Writer.Write(prepare, out pos));
			return prepare;
		}

		private PrepareLogRecord WriteTransactionEventV0(Guid correlationId, long logPosition, long transactionPosition,
			int transactionOffset, string eventStreamId, long eventNumber, string eventData, PrepareFlags flags) {
			var prepare = new PrepareLogRecord(logPosition, correlationId, Guid.NewGuid(), transactionPosition,
				transactionOffset,
				eventStreamId, ExpectedVersion.Any, DateTime.UtcNow, flags,
				"testEventType", Encoding.UTF8.GetBytes(eventData), new byte[0],
				LogRecordVersion.LogRecordV0);
			long pos;
			Writer.Write(prepare, out pos);
			return prepare;
		}

		private void WriteTransactionEndV0(Guid correlationId, long logPosition, long transactionId,
			string eventStreamId) {
			var prepare = new PrepareLogRecord(logPosition, correlationId, Guid.NewGuid(), transactionId, -1,
				eventStreamId, ExpectedVersion.Any,
				DateTime.UtcNow, PrepareFlags.TransactionEnd, null, new byte[0], new byte[0],
				LogRecordVersion.LogRecordV0);
			long pos;
			Writer.Write(prepare, out pos);
		}

		private long WriteCommitV0(Guid correlationId, long logPosition, long transactionPosition, string eventStreamId,
			long eventNumber, out long pos) {
			var commit = new CommitLogRecord(logPosition, correlationId, transactionPosition, DateTime.UtcNow,
				eventNumber,
				LogRecordVersion.LogRecordV0);

			Writer.Write(commit, out pos);
			return commit.LogPosition;
		}

		[Test]
		public void the_log_records_are_still_version_0_in_first_chunk() {
			var chunk = Db.Manager.GetChunk(0);

			var chunkRecords = new List<LogRecord>();
			RecordReadResult result = chunk.TryReadFirst();
			while (result.Success) {
				chunkRecords.Add(result.LogRecord);
				result = chunk.TryReadClosestForward(result.NextPosition);
			}

			Assert.IsTrue(chunkRecords.All(x => x.Version == LogRecordVersion.LogRecordV0));
			Assert.AreEqual(7, chunkRecords.Count);
		}

		[Test]
		public void the_log_records_are_unchanged_in_second_chunk() {
			var chunk = Db.Manager.GetChunk(1);

			var chunkRecords = new List<LogRecord>();
			RecordReadResult result = chunk.TryReadFirst();
			while (result.Success) {
				chunkRecords.Add(result.LogRecord);
				result = chunk.TryReadClosestForward(result.NextPosition);
			}

			Assert.IsTrue(chunkRecords.All(x => x.Version == LogRecordVersion.LogRecordV0));
			Assert.AreEqual(2, chunkRecords.Count);
		}

		public void return_correct_last_event_version_for_larger_stream() {
			Assert.AreEqual(2, ReadIndex.GetStreamLastEventNumber(_streamIdOne));
		}

		[Test]
		public void return_correct_first_record_for_larger_stream() {
			var result = ReadIndex.ReadEvent(_streamIdOne, 0);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_p1.EventId, result.Record.EventId);
		}

		[Test]
		public void return_correct_second_record_for_larger_stream() {
			var result = ReadIndex.ReadEvent(_streamIdOne, 1);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_p3.EventId, result.Record.EventId);
		}

		[Test]
		public void return_correct_third_record_for_larger_stream() {
			var result = ReadIndex.ReadEvent(_streamIdOne, 2);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_p5.EventId, result.Record.EventId);
		}

		[Test]
		public void not_find_record_with_nonexistent_version_for_larger_stream() {
			var result = ReadIndex.ReadEvent(_streamIdOne, 3);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void return_correct_range_on_from_start_range_query_for_larger_stream() {
			var result = ReadIndex.ReadStreamEventsForward(_streamIdOne, 0, 3);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(3, result.Records.Length);
			Assert.AreEqual(_p1.EventId, result.Records[0].EventId);
			Assert.AreEqual(_p3.EventId, result.Records[1].EventId);
			Assert.AreEqual(_p5.EventId, result.Records[2].EventId);
		}

		[Test]
		public void return_correct_range_on_from_end_range_query_for_larger_stream_with_specific_version() {
			var result = ReadIndex.ReadStreamEventsBackward(_streamIdOne, 2, 3);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(3, result.Records.Length);
			Assert.AreEqual(_p5.EventId, result.Records[0].EventId);
			Assert.AreEqual(_p3.EventId, result.Records[1].EventId);
			Assert.AreEqual(_p1.EventId, result.Records[2].EventId);
		}

		[Test]
		public void return_correct_range_on_from_end_range_query_for_larger_stream_with_from_end_version() {
			var result = ReadIndex.ReadStreamEventsBackward(_streamIdOne, -1, 3);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(3, result.Records.Length);
			Assert.AreEqual(_p5.EventId, result.Records[0].EventId);
			Assert.AreEqual(_p3.EventId, result.Records[1].EventId);
			Assert.AreEqual(_p1.EventId, result.Records[2].EventId);
		}

		[Test]
		public void return_correct_last_event_version_for_smaller_stream() {
			Assert.AreEqual(1, ReadIndex.GetStreamLastEventNumber(_streamIdTwo));
		}

		[Test]
		public void return_correct_first_record_for_smaller_stream() {
			var result = ReadIndex.ReadEvent(_streamIdTwo, 0);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_p2.EventId, result.Record.EventId);
		}

		[Test]
		public void return_correct_second_record_for_smaller_stream() {
			var result = ReadIndex.ReadEvent(_streamIdTwo, 1);
			Assert.AreEqual(ReadEventResult.Success, result.Result);
			Assert.AreEqual(_p4.EventId, result.Record.EventId);
		}

		[Test]
		public void not_find_record_with_nonexistent_version_for_smaller_stream() {
			var result = ReadIndex.ReadEvent(_streamIdTwo, 2);
			Assert.AreEqual(ReadEventResult.NotFound, result.Result);
			Assert.IsNull(result.Record);
		}

		[Test]
		public void return_correct_range_on_from_start_range_query_for_smaller_stream() {
			var result = ReadIndex.ReadStreamEventsForward(_streamIdTwo, 0, 2);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);
			Assert.AreEqual(_p2.EventId, result.Records[0].EventId);
			Assert.AreEqual(_p4.EventId, result.Records[1].EventId);
		}

		[Test]
		public void return_correct_range_on_from_end_range_query_for_smaller_stream_with_specific_version() {
			var result = ReadIndex.ReadStreamEventsBackward(_streamIdTwo, 1, 2);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);
			Assert.AreEqual(_p4.EventId, result.Records[0].EventId);
			Assert.AreEqual(_p2.EventId, result.Records[1].EventId);
		}

		[Test]
		public void return_correct_range_on_from_end_range_query_for_smaller_stream_with_from_end_version() {
			var result = ReadIndex.ReadStreamEventsBackward(_streamIdTwo, -1, 2);
			Assert.AreEqual(ReadStreamResult.Success, result.Result);
			Assert.AreEqual(2, result.Records.Length);
			Assert.AreEqual(_p4.EventId, result.Records[0].EventId);
			Assert.AreEqual(_p2.EventId, result.Records[1].EventId);
		}

		[Test]
		public void read_all_events_forward_returns_all_events_in_correct_order() {
			var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 10).Records;
			Assert.AreEqual(6, records.Count);
			Assert.AreEqual(_p2.EventId, records[0].Event.EventId);
			Assert.AreEqual(_p4.EventId, records[1].Event.EventId);
			Assert.AreEqual(_p1.EventId, records[2].Event.EventId);
			Assert.AreEqual(_p3.EventId, records[3].Event.EventId);
			Assert.AreEqual(_p5.EventId, records[4].Event.EventId);
			Assert.AreEqual(_random1.EventId, records[5].Event.EventId);
		}

		[Test]
		public void read_all_events_backward_returns_all_events_in_correct_order() {
			var pos = GetBackwardReadPos();
			var records = ReadIndex.ReadAllEventsBackward(pos, 10).Records;

			Assert.AreEqual(6, records.Count);
			Assert.AreEqual(_random1.EventId, records[0].Event.EventId);
			Assert.AreEqual(_p5.EventId, records[1].Event.EventId);
			Assert.AreEqual(_p3.EventId, records[2].Event.EventId);
			Assert.AreEqual(_p1.EventId, records[3].Event.EventId);
			Assert.AreEqual(_p4.EventId, records[4].Event.EventId);
			Assert.AreEqual(_p2.EventId, records[5].Event.EventId);
		}

		[Test]
		public void
			read_all_events_forward_returns_no_transaction_records_when_prepare_position_is_greater_than_last_prepare_in_commit() {
			var records = ReadIndex.ReadAllEventsForward(new TFPos(_t1CommitPos, _t1CommitPos), 10).Records;
			Assert.AreEqual(1, records.Count);
			Assert.AreEqual(_random1.EventId, records[0].Event.EventId);
		}

		[Test]
		public void
			read_all_events_backwards_returns_nothing_when_prepare_position_is_smaller_than_first_prepare_in_commit() {
			var records = ReadIndex.ReadAllEventsBackward(new TFPos(_t2CommitPos, 0), 10).Records;
			Assert.AreEqual(0, records.Count);
		}

		[Test]
		public void read_all_events_forward_returns_correct_events_starting_in_the_middle_of_tf() {
			var res1 = ReadIndex.ReadAllEventsForward(new TFPos(_t2CommitPos, _p4.LogPosition),
				10); // end of first commit
			Assert.AreEqual(5, res1.Records.Count);
			Assert.AreEqual(_p4.EventId, res1.Records[0].Event.EventId);
			Assert.AreEqual(_p1.EventId, res1.Records[1].Event.EventId);
			Assert.AreEqual(_p3.EventId, res1.Records[2].Event.EventId);
			Assert.AreEqual(_p5.EventId, res1.Records[3].Event.EventId);
			Assert.AreEqual(_random1.EventId, res1.Records[4].Event.EventId);

			var res2 = ReadIndex.ReadAllEventsBackward(res1.PrevPos, 10);
			Assert.AreEqual(1, res2.Records.Count);
			Assert.AreEqual(_p2.EventId, res2.Records[0].Event.EventId);
		}

		[Test]
		public void read_all_events_backward_returns_correct_events_starting_in_the_middle_of_tf() {
			var pos = new TFPos(_postCommitPos, _p4.LogPosition); // p3 post position
			var res1 = ReadIndex.ReadAllEventsBackward(pos, 10);

			Assert.AreEqual(4, res1.Records.Count);
			Assert.AreEqual(_p3.EventId, res1.Records[0].Event.EventId);
			Assert.AreEqual(_p1.EventId, res1.Records[1].Event.EventId);
			Assert.AreEqual(_p4.EventId, res1.Records[2].Event.EventId);
			Assert.AreEqual(_p2.EventId, res1.Records[3].Event.EventId);

			var res2 = ReadIndex.ReadAllEventsForward(res1.PrevPos, 10);
			Assert.AreEqual(2, res2.Records.Count);
			Assert.AreEqual(_p5.EventId, res2.Records[0].Event.EventId);
		}

		[Test]
		public void all_records_can_be_read_sequentially_page_by_page_in_forward_pass() {
			var recs = new[] {_p2, _p4, _p1, _p3, _p5, _random1}; // in committed order

			int count = 0;
			var pos = new TFPos(0, 0);
			IndexReadAllResult result;
			while ((result = ReadIndex.ReadAllEventsForward(pos, 1)).Records.Count != 0) {
				Assert.AreEqual(1, result.Records.Count);
				Assert.AreEqual(recs[count].EventId, result.Records[0].Event.EventId);
				pos = result.NextPos;
				count += 1;
			}

			Assert.AreEqual(recs.Length, count);
		}

		[Test]
		public void all_records_can_be_read_sequentially_page_by_page_in_backward_pass() {
			var recs = new[] {_random1, _p5, _p3, _p1, _p4, _p2}; // in reverse committed order

			int count = 0;
			var pos = GetBackwardReadPos();
			IndexReadAllResult result;
			while ((result = ReadIndex.ReadAllEventsBackward(pos, 1)).Records.Count != 0) {
				Assert.AreEqual(1, result.Records.Count);
				Assert.AreEqual(recs[count].EventId, result.Records[0].Event.EventId);
				pos = result.NextPos;
				count += 1;
			}

			Assert.AreEqual(recs.Length, count);
		}

		[Test]
		public void position_returned_for_prev_page_when_traversing_forward_allow_to_traverse_backward_correctly() {
			var recs = new[] {_p2, _p4, _p1, _p3, _p5, _random1}; // in committed order

			int count = 0;
			var pos = new TFPos(0, 0);
			IndexReadAllResult result;
			while ((result = ReadIndex.ReadAllEventsForward(pos, 1)).Records.Count != 0) {
				Assert.AreEqual(1, result.Records.Count);
				Assert.AreEqual(recs[count].EventId, result.Records[0].Event.EventId);

				var localPos = result.PrevPos;
				int localCount = 0;
				IndexReadAllResult localResult;
				while ((localResult = ReadIndex.ReadAllEventsBackward(localPos, 1)).Records.Count != 0) {
					Assert.AreEqual(1, localResult.Records.Count);
					Assert.AreEqual(recs[count - 1 - localCount].EventId, localResult.Records[0].Event.EventId);
					localPos = localResult.NextPos;
					localCount += 1;
				}

				pos = result.NextPos;
				count += 1;
			}

			Assert.AreEqual(recs.Length, count);
		}

		[Test]
		public void position_returned_for_prev_page_when_traversing_backward_allow_to_traverse_forward_correctly() {
			var recs = new[] {_random1, _p5, _p3, _p1, _p4, _p2}; // in reverse committed order

			int count = 0;
			var pos = GetBackwardReadPos();
			IndexReadAllResult result;
			while ((result = ReadIndex.ReadAllEventsBackward(pos, 1)).Records.Count != 0) {
				Assert.AreEqual(1, result.Records.Count);
				Assert.AreEqual(recs[count].EventId, result.Records[0].Event.EventId);

				var localPos = result.PrevPos;
				int localCount = 0;
				IndexReadAllResult localResult;
				while ((localResult = ReadIndex.ReadAllEventsForward(localPos, 1)).Records.Count != 0) {
					Assert.AreEqual(1, localResult.Records.Count);
					Assert.AreEqual(recs[count - 1 - localCount].EventId, localResult.Records[0].Event.EventId);
					localPos = localResult.NextPos;
					localCount += 1;
				}

				pos = result.NextPos;
				count += 1;
			}

			Assert.AreEqual(recs.Length, count);
		}
	}
}
