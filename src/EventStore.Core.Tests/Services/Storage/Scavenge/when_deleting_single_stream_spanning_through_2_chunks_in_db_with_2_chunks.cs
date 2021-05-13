using System.Linq;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class when_deleting_single_stream_spanning_through_2_chunks_in_db_with_2_chunks<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		private EventRecord _event3;
		private EventRecord _event4;
		private EventRecord _delete;

		protected override void WriteTestScenario() {
			WriteSingleEvent("ES", 0, new string('.', 3000));
			WriteSingleEvent("ES", 1, new string('.', 3000));
			WriteSingleEvent("ES", 2, new string('.', 3000));

			_event3 = WriteSingleEvent("ES", 3, new string('.', 3000), retryOnFail: true); // chunk 2
			_event4 = WriteSingleEvent("ES", 4, new string('.', 3000));

			_delete = WriteDelete("ES");
			Scavenge(completeLast: false, mergeChunks: false);
		}

		[Test]
		public void read_all_forward_returns_events_only_from_uncompleted_chunk_and_delete_record() {
			var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(3, events.Length);
			Assert.AreEqual(_event3, events[0]);
			Assert.AreEqual(_event4, events[1]);
			Assert.AreEqual(_delete, events[2]);
		}

		[Test]
		public void read_all_backward_returns_events_only_from_uncompleted_chunk_and_delete_record() {
			var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event)
				.ToArray();
			Assert.AreEqual(3, events.Length);
			Assert.AreEqual(_event3, events[2]);
			Assert.AreEqual(_event4, events[1]);
			Assert.AreEqual(_delete, events[0]);
		}

		[Test]
		public void read_all_backward_from_beginning_of_second_chunk_returns_no_records() {
			var pos = new TFPos(10000, 10000);
			var events = ReadIndex.ReadAllEventsBackward(pos, 100).Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(0, events.Length);
		}

		[Test]
		public void read_all_forward_from_beginning_of_second_chunk_with_max_1_record_returns_5th_record() {
			var events = ReadIndex.ReadAllEventsForward(new TFPos(10000, 10000), 1).Records.Select(r => r.Event)
				.ToArray();
			Assert.AreEqual(1, events.Length);
			Assert.AreEqual(_event3, events[0]);
		}

		[Test]
		public void read_all_forward_with_max_5_records_returns_3_records_from_second_chunk_and_delete_record() {
			var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 5).Records.Select(r => r.Event).ToArray();
			Assert.AreEqual(3, events.Length);
			Assert.AreEqual(_event3, events[0]);
			Assert.AreEqual(_event4, events[1]);
			Assert.AreEqual(_delete, events[2]);
		}

		[Test]
		public void is_stream_deleted_returns_true() {
			Assert.That(ReadIndex.IsStreamDeleted("ES"));
		}

		[Test]
		public void last_event_number_returns_stream_deleted() {
			Assert.AreEqual(EventNumber.DeletedStream, ReadIndex.GetStreamLastEventNumber("ES"));
		}
	}
}
