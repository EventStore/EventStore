using System;
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage
{
    [TestFixture]
    public class when_having_stream_both_maxage_and_maxcount_specified_with_maxage_more_strict : ReadIndexTestScenario
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

            long curPos;
            const string metadata = @"{""$maxAge"":10,""$maxCount"":4}";

            var streamCreated = new PrepareLogRecord(0,
                                                     Guid.NewGuid(),
                                                     Guid.NewGuid(),
                                                     0,
                                                     0,
                                                     "ES",
                                                     ExpectedVersion.NoStream,
                                                     now.AddSeconds(-100),
                                                     PrepareFlags.Data | PrepareFlags.TransactionBegin | PrepareFlags.TransactionEnd,
                                                     SystemEventTypes.StreamCreated,
                                                     LogRecord.NoData,
                                                     Encoding.UTF8.GetBytes(metadata));
            _r1 = new EventRecord(0, streamCreated);
            Writer.Write(streamCreated, out curPos);
            Writer.Write(LogRecord.Commit(curPos, Guid.NewGuid(), 0, 0), out curPos);

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
        public void single_event_read_doesnt_return_expired_events_and_returns_all_actual_ones()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 0, out record));
            Assert.IsNull(record);
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 1, out record));
            Assert.IsNull(record);
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 2, out record));
            Assert.IsNull(record);
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 3, out record));
            Assert.IsNull(record);

            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 4, out record));
            Assert.AreEqual(_r5, record);
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 5, out record));
            Assert.AreEqual(_r6, record);
        }

        [Test]
        public void forward_range_read_doesnt_return_expired_records()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("ES", 0, 100, out records));
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(_r5, records[0]);
            Assert.AreEqual(_r6, records[1]);
        }

        [Test]
        public void backward_range_read_doesnt_return_expired_records()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ES", -1, 100, out records));
            Assert.AreEqual(2, records.Length);
            Assert.AreEqual(_r6, records[0]);
            Assert.AreEqual(_r5, records[1]);
        }
    }
}