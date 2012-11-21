using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
    public class when_deleting_single_stream_spanning_through_2_chunks_in_2nd_chunk__in_db_with_3_chunks : ReadIndexTestScenario
    {
        private EventRecord _event1;
        private EventRecord _event2;
        private EventRecord _event3;
        private EventRecord _event4;
        private EventRecord _event5;
        private EventRecord _event6;
        private PrepareLogRecord _event7prepare;
        private CommitLogRecord _event7commit;

        private EventRecord _event8;
        private EventRecord _event9;

        protected override void WriteTestScenario()
        {
            _event1 = WriteStreamCreated("ES");                                            // chunk 1

            _event2 = WriteSingleEvent("ES", 1, new string('.', 3000));
            _event3 = WriteSingleEvent("ES", 2, new string('.', 3000));
            _event4 = WriteSingleEvent("ES", 3, new string('.', 3000));

            _event5 = WriteSingleEvent("ES", 4, new string('.', 3000), retryOnFail: true); // chunk 2
            _event6 = WriteSingleEvent("ES", 5, new string('.', 3000));

            _event7prepare = WriteDeletePrepare("ES");
            _event7commit = WriteDeleteCommit(_event7prepare);

            _event8 = WriteStreamCreated("ES2");
            _event9 = WriteSingleEvent("ES2", 1, new string('.', 5000), retryOnFail: true); //chunk 3

            Scavenge();
        }

        private PrepareLogRecord WriteDeletePrepare(string eventStreamId)
        {
            var prepare = LogRecord.DeleteTombstone(WriterChecksum.ReadNonFlushed(),
                                                           Guid.NewGuid(),
                                                           eventStreamId,
                                                           ExpectedVersion.Any);
            long pos;
            Assert.IsTrue(Writer.Write(prepare, out pos));

            return prepare;
        }

        private CommitLogRecord WriteDeleteCommit(PrepareLogRecord prepare)
        {
            long pos;
            var commit = LogRecord.Commit(WriterChecksum.ReadNonFlushed(),
                                          prepare.CorrelationId,
                                          prepare.LogPosition,
                                          EventNumber.DeletedStream);
            Assert.IsTrue(Writer.Write(commit, out pos));

            return commit;
        }

        [Test]
        public void read_all_forward_does_not_return_scavenged_deleted_stream_events_and_return_remaining()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(3, events.Length);
            Assert.AreEqual(_event1, events[0]);
            Assert.AreEqual(_event8, events[1]);
            Assert.AreEqual(_event9, events[2]);
        }

        [Test]
        public void read_all_backward_does_not_return_scavenged_deleted_stream_events_and_return_remaining()
        {
            var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(3, events.Length);
            Assert.AreEqual(_event1, events[2]);
            Assert.AreEqual(_event8, events[1]);
            Assert.AreEqual(_event9, events[0]);
        }

        [Test]
        public void read_all_backward_from_beginning_of_second_chunk_returns_no_records()
        {
            var pos = new TFPos(10000, 10000);
            var events = ReadIndex.ReadAllEventsBackward(pos, 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(_event1, events[0]);
        }

        [Test]
        public void read_all_forward_from_beginning_of_2nd_chunk_with_max_1_record_returns_1st_record_from_3rd_chunk()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(10000, 10000), 1).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(_event8, events[0]);
        }

        [Test]
        public void read_all_forward_with_max_5_records_returns_2_records_from_2nd_chunk()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 5).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(3, events.Length);
            Assert.AreEqual(_event1, events[0]);
            Assert.AreEqual(_event8, events[1]);
            Assert.AreEqual(_event9, events[2]);
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

        [Test]
        public void last_physical_record_from_scavenged_stream_should_remain()
        {
            // cannot use readIndex here as it doesn't return deleteTombstone

            var chunk = Db.Manager.GetChunk(1);
            var chunkPos = (int) (_event7prepare.LogPosition%Db.Config.ChunkSize);
            var res = chunk.TryReadAt(chunkPos);

            Assert.IsTrue(res.Success);
            Assert.AreEqual(_event7prepare, res.LogRecord);

            chunkPos = (int)(_event7commit.LogPosition % Db.Config.ChunkSize);
            res = chunk.TryReadAt(chunkPos);

            Assert.IsTrue(res.Success);
            Assert.AreEqual(_event7commit, res.LogRecord);
        }
    }
}
