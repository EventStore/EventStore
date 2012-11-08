using System.Linq;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
    public class when_deleting_single_stream_spanning_through_2_chunks_in_db_with_2_chunks :ReadIndexTestScenario
    {
        private EventRecord _event1;
        private EventRecord _event2;
        private EventRecord _event3;
        private EventRecord _event4;
        private EventRecord _event5;
        private EventRecord _event6;

        protected override void WriteTestScenario()
        {
            _event1 = WriteStreamCreated("ES");

            _event2 = WriteSingleEvent("ES", 1, new string('.', 3000));
            _event3 = WriteSingleEvent("ES", 2, new string('.', 3000));
            _event4 = WriteSingleEvent("ES", 3, new string('.', 3000));
            
            _event5 = WriteSingleEvent("ES", 4, new string('.', 3000), retryOnFail: true); // chunk 2
            _event6 = WriteSingleEvent("ES", 5, new string('.', 3000));

            WriteDelete("ES");
            Scavenge();
        }

        [Test]
        public void read_all_forward_returns_events_only_from_uncompleted_chunk()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(_event5, events[0]);
            Assert.AreEqual(_event6, events[1]);
        }

        [Test]
        public void read_all_backward_returns_events_only_from_uncompleted_chunk()
        {
            var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(_event5, events[1]);
            Assert.AreEqual(_event6, events[0]);
        }

        [Test]
        public void read_all_backward_from_beginning_of_second_chunk_returns_no_records()
        {
            var pos = new TFPos(10000, 10000);
            var events = ReadIndex.ReadAllEventsBackward(pos, 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(0, events.Length);
        }

        [Test]
        public void read_all_forward_from_beginning_of_second_chunk_with_max_1_record_returns_5th_record()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(10000, 10000), 1).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(_event5, events[0]);
        }

        [Test]
        public void read_all_forward_with_max_5_records_returns_2_records_from_second_chunk()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0,0), 5).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(_event5, events[0]);
            Assert.AreEqual(_event6, events[1]);
        }

        [Test]
        public void is_stream_deleted_returns_true()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ES"));
        }

        [Test]
        public void last_event_number_returns_stream_deleted()
        {
            Assert.AreEqual(EventNumber.DeletedStream, ReadIndex.GetLastStreamEventNumber("ES"));
        }

    }
}
