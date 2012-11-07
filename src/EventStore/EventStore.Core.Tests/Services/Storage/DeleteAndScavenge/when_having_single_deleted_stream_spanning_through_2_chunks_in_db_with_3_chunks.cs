using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Core.Data;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeleteAndScavenge
{
    [TestFixture]
    public class when_having_single_deleted_stream_spanning_through_2_chunks_in_db_with_3_chunks :ReadIndexTestScenario
    {
        private EventRecord _event1;
        private EventRecord _event2;
        private EventRecord _event3;
        private EventRecord _event4;
        private EventRecord _event5;
        private EventRecord _event6;

        private EventRecord _event7;
        private EventRecord _event8;

        protected override void WriteTestScenario()
        {
            _event1 = WriteStreamCreated("ES");

            _event2 = WriteSingleEvent("ES", 1, new string('.', 3000));
            _event3 = WriteSingleEvent("ES", 2, new string('.', 3000));
            _event4 = WriteSingleEvent("ES", 3, new string('.', 3000));
            
            _event5 = WriteSingleEvent("ES", 4, new string('.', 3000), retryOnFail: true); // chunk 2
            _event6 = WriteSingleEvent("ES", 5, new string('.', 3000));

            

            _event7 = WriteStreamCreated("ES2");
            _event8 = WriteSingleEvent("ES2", 1, new string('.', 5000), retryOnFail: true); //chunk 3

            WriteDelete("ES");
            Scavenge();
        }

        [Test]
        public void read_all_forward_should_not_return_scavenged_deleted_stream_events_and_return_remaining()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(_event7, events[0]);
            Assert.AreEqual(_event8, events[1]);
        }
    }
}
