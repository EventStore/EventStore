using System;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount
{
    [TestFixture]
    public class when_having_stream_with_maxcount_specified : ReadIndexTestScenario
    {
        private EventRecord _r1;
        private EventRecord _r2;
        private EventRecord _r3;
        private EventRecord _r4;
        private EventRecord _r5;
        private EventRecord _r6;

        protected override void WriteTestScenario()
        {
            var now = DateTime.UtcNow;
            const string metadata = @"{""$maxCount"":4}";
            
            _r1 = WriteStreamCreated("ES", metadata, now.AddSeconds(-100));
            _r2 = WriteSingleEvent("ES", 1, "bla1", now.AddSeconds(-50));
            _r3 = WriteSingleEvent("ES", 2, "bla1", now.AddSeconds(-20));
            _r4 = WriteSingleEvent("ES", 3, "bla1", now.AddSeconds(-11));
            _r5 = WriteSingleEvent("ES", 4, "bla1", now.AddSeconds(-5));
            _r6 = WriteSingleEvent("ES", 5, "bla1", now.AddSeconds(-1));
        }

        [Test]
        public void single_event_read_doesnt_return_stream_created_event()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 0, out record));
            Assert.IsNull(record);
        }

        [Test]
        public void single_event_read_doesnt_return_old_events_and_return_actual_ones()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 0, out record));
            Assert.IsNull(record);
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 1, out record));
            Assert.IsNull(record);

            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 2, out record));
            Assert.AreEqual(_r3, record);
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 3, out record));
            Assert.AreEqual(_r4, record);
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 4, out record));
            Assert.AreEqual(_r5, record);
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 5, out record));
            Assert.AreEqual(_r6, record);
        }

        [Test]
        public void forward_range_read_doesnt_return_old_records()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("ES", 0, 100, out records));
            Assert.AreEqual(4, records.Length);
            Assert.AreEqual(_r3, records[0]);
            Assert.AreEqual(_r4, records[1]);
            Assert.AreEqual(_r5, records[2]);
            Assert.AreEqual(_r6, records[3]);
        }

        [Test]
        public void backward_range_read_doesnt_return_expired_records()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ES", -1, 100, out records));
            Assert.AreEqual(4, records.Length);
            Assert.AreEqual(_r6, records[0]);
            Assert.AreEqual(_r5, records[1]);
            Assert.AreEqual(_r4, records[2]);
            Assert.AreEqual(_r3, records[3]);
        }

        [Test]
        public void read_all_forward_returns_all_records_including_expired_ones()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
            Assert.AreEqual(6, records.Count);
            Assert.AreEqual(_r1, records[0].Event);
            Assert.AreEqual(_r2, records[1].Event);
            Assert.AreEqual(_r3, records[2].Event);
            Assert.AreEqual(_r4, records[3].Event);
            Assert.AreEqual(_r5, records[4].Event);
            Assert.AreEqual(_r6, records[5].Event);
        }

        [Test]
        public void read_all_backward_returns_all_records_including_expired_ones()
        {
            var pos = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            var records = ReadIndex.ReadAllEventsBackward(pos, 100).Records;
            Assert.AreEqual(6, records.Count);
            Assert.AreEqual(_r6, records[0].Event);
            Assert.AreEqual(_r5, records[1].Event);
            Assert.AreEqual(_r4, records[2].Event);
            Assert.AreEqual(_r3, records[3].Event);
            Assert.AreEqual(_r2, records[4].Event);
            Assert.AreEqual(_r1, records[5].Event);
        }
    }
}