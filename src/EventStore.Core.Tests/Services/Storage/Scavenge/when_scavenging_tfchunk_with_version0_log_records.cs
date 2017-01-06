using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
    public class when_scavenging_tfchunk_with_version0_log_records_and_incomplete_chunk : ReadIndexTestScenario
    {
        private string _eventStreamId = "ES";
        private LogRecord[] _firstChunkRecords, _secondChunkRecords;

        protected override void WriteTestScenario()
        {
            _firstChunkRecords = new LogRecord[11];
            _secondChunkRecords = new LogRecord[2];

            for(var i = 0; i < 11; i++) { // Chunk 1 with 11 events
                _firstChunkRecords[i] = WriteSingleEventWithLogVersion0(Guid.NewGuid(), _eventStreamId, WriterCheckpoint.ReadNonFlushed(), i);
            }
            Writer.CompleteChunk();

            for(var i = 0; i < 2; i++) { // Incomplete chunk with 2 events
                _secondChunkRecords[i] = WriteSingleEventWithLogVersion0(Guid.NewGuid(), _eventStreamId, WriterCheckpoint.ReadNonFlushed(), i + 10);
            }

            Scavenge(completeLast: false, mergeChunks: true);
        }

        [Test]
        public void should_be_able_to_read_the_stream()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(13, events.Length);
        }

        [Test]
        public void the_new_log_records_are_version_1_in_first_chunk()
        {
            var chunk = Db.Manager.GetChunk(0);

            var chunkRecords = new List<LogRecord>();
            RecordReadResult result = chunk.TryReadFirst();
            while (result.Success)
            {
                chunkRecords.Add(result.LogRecord);
                result = chunk.TryReadClosestForward(result.NextPosition);
            }
            Assert.IsTrue(chunkRecords.All(x=>x.Version == LogRecordVersion.LogRecordV1));
            Assert.AreEqual(22, chunkRecords.Count);
        }

        [Test]
        public void the_log_records_in_the_second_chunk_should_be_unchanged()
        {
            var chunk = Db.Manager.GetChunk(1);

            var chunkRecords = new List<LogRecord>();
            RecordReadResult result = chunk.TryReadFirst();
            while (result.Success)
            {
                chunkRecords.Add(result.LogRecord);
                result = chunk.TryReadClosestForward(result.NextPosition);
            }
            Assert.IsTrue(chunkRecords.All(x=>x.Version == LogRecordVersion.LogRecordV0));
            Assert.AreEqual(4, chunkRecords.Count);
        }
    }
}
