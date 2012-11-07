using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeleteAndScavenge
{
    [TestFixture]
    public class when_having_single_deleted_stream_spanning_through_2_chunks :ReadIndexTestScenario
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
        public void read_all_forward_should_return_events_only_from_uncompleted_chunk()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(_event5, events[0]);
            Assert.AreEqual(_event6, events[1]);
        }
    }
}
