using System;
using EventStore.Core.Data;
using EventStore.Core.Services;
using NUnit.Framework;
using EventStore.Core.TransactionLog.LogRecords;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount
{
    [TestFixture]
    public class with_truncatebefore_greater_than_int_maxvalue : ReadIndexTestScenario
    {
        private EventRecord _r1;
        private PrepareLogRecord _r2;
        private PrepareLogRecord _r3;
        private PrepareLogRecord _r4;
        private PrepareLogRecord _r5;
        private PrepareLogRecord _r6;

        private const long first = (long)int.MaxValue + 1;
        private const long second = (long)int.MaxValue + 2;
        private const long third = (long)int.MaxValue + 3;
        private const long fourth = (long)int.MaxValue + 4;
        private const long fifth = (long)int.MaxValue + 5;

        protected override void WriteTestScenario()
        {
            var now = DateTime.UtcNow;

            string metadata = @"{""$tb"":" + third + "}";

// Guid id, string streamId, long position, long expectedVersion, PrepareFlags? flags = null
            _r1 = WriteStreamMetadata("ES", 0, metadata, now.AddSeconds(-100));
            _r2 = WriteSingleEventWithLogVersion1(Guid.NewGuid(), "ES", WriterCheckpoint.ReadNonFlushed(), first);
            _r3 = WriteSingleEventWithLogVersion1(Guid.NewGuid(), "ES", WriterCheckpoint.ReadNonFlushed(), second);
            _r4 = WriteSingleEventWithLogVersion1(Guid.NewGuid(), "ES", WriterCheckpoint.ReadNonFlushed(), third);
            _r5 = WriteSingleEventWithLogVersion1(Guid.NewGuid(), "ES", WriterCheckpoint.ReadNonFlushed(), fourth);
            _r6 = WriteSingleEventWithLogVersion1(Guid.NewGuid(), "ES", WriterCheckpoint.ReadNonFlushed(), fifth);
        }

        [Test]
        public void metastream_read_returns_metaevent()
        {
            var result = ReadIndex.ReadEvent(SystemStreams.MetastreamOf("ES"), 0);
            Assert.AreEqual(ReadEventResult.Success, result.Result);
            Assert.AreEqual(_r1, result.Record);
        }

        [Test]
        public void single_event_read_returns_records_after_truncate_before()
        {
            var result = ReadIndex.ReadEvent("ES", first);
            Assert.AreEqual(ReadEventResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES", second);
            Assert.AreEqual(ReadEventResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES", third);
            Assert.AreEqual(ReadEventResult.Success, result.Result);
            Assert.AreEqual(_r4.EventId, result.Record.EventId);

            result = ReadIndex.ReadEvent("ES", fourth);
            Assert.AreEqual(ReadEventResult.Success, result.Result);
            Assert.AreEqual(_r5.EventId, result.Record.EventId);

            result = ReadIndex.ReadEvent("ES", fifth);
            Assert.AreEqual(ReadEventResult.Success, result.Result);
            Assert.AreEqual(_r6.EventId, result.Record.EventId);
        }

        [Test]
        public void forward_range_read_returns_records_after_truncate_before()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES", first, 100);
            Assert.AreEqual(ReadStreamResult.Success, result.Result);
            Assert.AreEqual(3, result.Records.Length);
            Assert.AreEqual(_r4.EventId, result.Records[0].EventId);
            Assert.AreEqual(_r5.EventId, result.Records[1].EventId);
            Assert.AreEqual(_r6.EventId, result.Records[2].EventId);
        }

        [Test]
        public void backward_range_read_returns_records_after_truncate_before()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
            Assert.AreEqual(ReadStreamResult.Success, result.Result);
            Assert.AreEqual(3, result.Records.Length);
            Assert.AreEqual(_r6.EventId, result.Records[0].EventId);
            Assert.AreEqual(_r5.EventId, result.Records[1].EventId);
            Assert.AreEqual(_r4.EventId, result.Records[2].EventId);
        }

        [Test]
        public void read_all_forward_returns_all_records()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
            Assert.AreEqual(6,   records.Count);
            Assert.AreEqual(_r1.EventId, records[0].Event.EventId);
            Assert.AreEqual(_r2.EventId, records[1].Event.EventId);
            Assert.AreEqual(_r3.EventId, records[2].Event.EventId);
            Assert.AreEqual(_r4.EventId, records[3].Event.EventId);
            Assert.AreEqual(_r5.EventId, records[4].Event.EventId);
            Assert.AreEqual(_r6.EventId, records[5].Event.EventId);
        }

        [Test]
        public void read_all_backward_returns_all_records()
        {
            var records = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records;
            Assert.AreEqual(6,   records.Count);
            Assert.AreEqual(_r6.EventId, records[0].Event.EventId);
            Assert.AreEqual(_r5.EventId, records[1].Event.EventId);
            Assert.AreEqual(_r4.EventId, records[2].Event.EventId);
            Assert.AreEqual(_r3.EventId, records[3].Event.EventId);
            Assert.AreEqual(_r2.EventId, records[4].Event.EventId);
            Assert.AreEqual(_r1.EventId, records[5].Event.EventId);
        }
    } 
}