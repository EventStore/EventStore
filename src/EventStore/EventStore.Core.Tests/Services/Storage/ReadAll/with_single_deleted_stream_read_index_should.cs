using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.ReadAll
{
    [TestFixture]
    class with_single_deleted_stream_read_index_should : ReadIndexTestScenario
    {
        private EventRecord _event;
        private EventRecord _delete;

        protected override void WriteTestScenario()
        {
            _event = WriteSingleEvent("ES", 0, "test1");
            _delete = WriteDelete("ES");
        }

        [Test]
        public void return_deleted_events_but_exclude_delete_event_when_reading_all_forward()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
            Assert.AreEqual(1, records.Count);
            Assert.That(records[0].Event.Equals(_event));
        }

        [Test]
        public void return_deleted_events_but_exclude_delete_event_when_reading_all_backward()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
            Assert.AreEqual(1, records.Count);
            Assert.That(records[0].Event.Equals(_event));
        }

    }
}
