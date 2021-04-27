using System.Linq;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Truncation {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class
		when_truncating_single_uncompleted_chunk_with_index_in_memory_and_then_reopening_db<TLogFormat, TStreamId> :
			TruncateAndReOpenDbScenario<TLogFormat, TStreamId> {
		private EventRecord _event1;
		private EventRecord _event2;
		private EventRecord _event3;

		public when_truncating_single_uncompleted_chunk_with_index_in_memory_and_then_reopening_db()
			: base(20000) {
		}

		protected override void WriteTestScenario() {
			_event1 = WriteSingleEvent("ES", 0, new string('.', 500));
			_event2 = WriteSingleEvent("ES", 1, new string('.', 500)); // truncated
			_event3 = WriteSingleEvent("ES", 2, new string('.', 500)); // truncated

			TruncateCheckpoint = _event2.LogPosition;
		}

		[Test]
		public void checksums_should_be_equal_to_ack_checksum() {
			Assert.AreEqual(TruncateCheckpoint, WriterCheckpoint.Read());
			Assert.AreEqual(TruncateCheckpoint, ChaserCheckpoint.Read());
		}

		[Test]
		public void read_one_by_one_doesnt_return_truncated_records() {
			var res = ReadIndex.ReadEvent("ES", 0);
			Assert.AreEqual(ReadEventResult.Success, res.Result);
			Assert.AreEqual(_event1, res.Record);

			res = ReadIndex.ReadEvent("ES", 1);
			Assert.AreEqual(ReadEventResult.NotFound, res.Result);
			Assert.IsNull(res.Record);

			res = ReadIndex.ReadEvent("ES", 2);
			Assert.AreEqual(ReadEventResult.NotFound, res.Result);
			Assert.IsNull(res.Record);

			res = ReadIndex.ReadEvent("ES", 3);
			Assert.AreEqual(ReadEventResult.NotFound, res.Result);
			Assert.IsNull(res.Record);
		}

		[Test]
		public void read_stream_forward_doesnt_return_truncated_records() {
			var res = ReadIndex.ReadStreamEventsForward("ES", 0, 100);
			var records = res.Records;
			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event1, records[0]);
		}

		[Test]
		public void read_stream_backward_doesnt_return_truncated_records() {
			var res = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
			var records = res.Records;
			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event1, records[0]);
		}

		[Test]
		public void read_all_forward_doesnt_return_truncated_records() {
			var res = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100);
			var records = res.Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event1, records[0]);
		}

		[Test]
		public void read_all_backward_doesnt_return_truncated_records() {
			var res = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100);
			var records = res.Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(1, records.Length);
			Assert.AreEqual(_event1, records[0]);
		}

		[Test]
		public void read_all_backward_from_last_truncated_record_returns_no_records() {
			var pos = new TFPos(_event3.LogPosition, _event3.LogPosition);
			var res = ReadIndex.ReadAllEventsForward(pos, 100);
			var records = res.Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(0, records.Length);
		}
	}
}
