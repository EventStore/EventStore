using System;
using NUnit.Framework;
using EventStore.Core.Data;

namespace EventStore.Core.Tests.Services.Storage.AllReader
{
    [TestFixture]
    public class when_reading_all_with_replication_checkpoint_set 
        : ReadIndexTestScenario
    {
        long _commitPosition;

        protected override void WriteTestScenario()
        {
            var res = WritePrepare("ES1", 0, Guid.NewGuid(), "event-type", new string('.', 3000));
            WriteCommit(res.LogPosition, "ES1", 0);

            res = WritePrepare("ES2", 0, Guid.NewGuid(), "event-type", new string('.', 3000));
            var commit = WriteCommit(res.LogPosition, "ES2", 0);
            _commitPosition = commit.LogPosition;

            res = WritePrepare("ES2", 1, Guid.NewGuid(), "event-type", new string('.', 3000));
            WriteCommit(res.LogPosition, "ES2", 1);

            ReplicationCheckpoint.Write(_commitPosition);
        }

        [Test]
        public void should_be_able_to_read_replicated_stream_backwards()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES1", 9, 10);
            Assert.AreEqual(1, result.Records.Length);
        }

        [Test]
        public void should_be_able_to_read_replicated_stream_forwards()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES1", 0, 10);
            Assert.AreEqual(1, result.Records.Length);
        }

        [Test]
        public void should_be_able_to_read_events_before_replication_checkpoint_backwards()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES2", 9, 10);
            Assert.AreEqual(1, result.Records.Length);
            Assert.AreEqual(0, result.Records[0].EventNumber);
        }
        
        [Test]
        public void should_be_able_to_read_events_before_replication_checkpoint_forward()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES2", 0, 10);
            Assert.AreEqual(1, result.Records.Length);
            Assert.AreEqual(0, result.Records[0].EventNumber);
        }

        [Test]
        public void should_be_able_to_read_single_event_before_replication_checkpoint()
        {
            var result = ReadIndex.ReadEvent("ES2", 0);
            Assert.AreEqual(ReadEventResult.Success, result.Result);
        }

        [Test]
        public void should_not_be_able_to_read_single_event_after_replication_checkpoint()
        {
            var result = ReadIndex.ReadEvent("ES2", 1);
            Assert.AreEqual(ReadEventResult.NotFound, result.Result);
        }


        [Test]
        public void should_be_able_to_read_all_backwards_and_get_events_before_replication_checkpoint()
        {
            var checkpoint = WriterCheckpoint.Read();
            var pos = new TFPos(checkpoint, checkpoint);
            var result = ReadIndex.ReadAllEventsBackward(pos, 10);
            Assert.AreEqual(2, result.Records.Count);
        }

        [Test]
        public void should_be_able_to_read_all_forwards_and_get_events_before_replication_checkpoint()
        {
            var result = ReadIndex.ReadAllEventsForward(new TFPos(0,0), 10);
            Assert.AreEqual(2, result.Records.Count);
        }
    }
}