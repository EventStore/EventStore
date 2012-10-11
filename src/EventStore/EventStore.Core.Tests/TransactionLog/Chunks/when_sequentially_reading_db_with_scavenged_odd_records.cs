using System;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_sequentially_reading_db_with_scavenged_odd_records: SpecificationWithDirectoryPerTestFixture
    {
        private const int RecordsCount = 16;

        private TFChunkDb _db;
        private LogRecord[] _records;
        private LogRecord[] _keptRecords;
        private RecordWriteResult[] _results;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                    new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                    4096,
                                                    0,
                                                    new InMemoryCheckpoint(),
                                                    new ICheckpoint[0]));
            _db.OpenVerifyAndClean();

            var chunk = _db.Manager.GetChunk(0);

            _records = new LogRecord[RecordsCount];
            _results = new RecordWriteResult[RecordsCount];

            var pos = 0;
            for (int i = 0; i < RecordsCount; ++i)
            {
                if (i > 0 && i % 3 == 0)
                {
                    pos = i/3 * _db.Config.ChunkSize;
                    chunk.Complete();
                    chunk = _db.Manager.AddNewChunk();
                }

                _records[i] = LogRecord.SingleWrite(pos,
                                                    Guid.NewGuid(), Guid.NewGuid(), i%2 == 1 ? "es-to-scavenge": "es1", 
                                                    ExpectedVersion.Any, "et1", new byte[1200], new byte[] { 5, 7 });
                _results[i] = chunk.TryAppend(_records[i]);

                pos += _records[i].GetSizeWithLengthPrefixAndSuffix();
            }

            _keptRecords = _records.Where((x, i) => i%2 == 0).ToArray();

            chunk.Flush();
            chunk.Complete();
            _db.Config.WriterCheckpoint.Write((RecordsCount / 3) * _db.Config.ChunkSize + _results[RecordsCount - 1].NewPosition);
            _db.Config.WriterCheckpoint.Flush();

            var scavenger = new TFChunkScavenger(_db, new FakeReadIndex(x => x == "es-to-scavenge"));
            scavenger.Scavenge(alwaysKeepScavenged: true);
        }

        public override void TestFixtureTearDown()
        {
            _db.Dispose();

            base.TestFixtureTearDown();
        }

        [Test]
        public void all_records_were_written()
        {
            var pos = 0;
            for (int i = 0; i < RecordsCount; ++i)
            {
                if (i % 3 == 0)
                    pos = 0;

                Assert.IsTrue(_results[i].Success);
                Assert.AreEqual(pos, _results[i].OldPosition);

                pos += _records[i].GetSizeWithLengthPrefixAndSuffix();
                Assert.AreEqual(pos, _results[i].NewPosition);
            }
        }

        [Test]
        public void all_records_could_be_read_with_forward_pass()
        {
            var seqReader = new TFChunkSequentialReader(_db, _db.Config.WriterCheckpoint, 0);

            RecordReadResult res;
            int count = 0;
            while ((res = seqReader.TryReadNext()).Success)
            {
                var rec = _keptRecords[count];
                Assert.AreEqual(rec, res.LogRecord);

                ++count;
            }
            Assert.AreEqual(_keptRecords.Length, count);
        }

        [Test]
        public void all_records_could_be_read_with_backwards_pass()
        {
            var seqReader = new TFChunkSequentialReader(_db, _db.Config.WriterCheckpoint, _db.Config.WriterCheckpoint.Read());

            RecordReadResult res;
            int count = 0;
            while ((res = seqReader.TryReadPrev()).Success)
            {
                var rec = _keptRecords[_keptRecords.Length - count - 1];
                Assert.AreEqual(rec, res.LogRecord);
                Assert.AreEqual(rec.Position, seqReader.Position);

                ++count;
            }
            Assert.AreEqual(_keptRecords.Length, count);
        }

        [Test]
        public void all_records_could_be_read_doing_forward_backward_pass()
        {
            var seqReader = new TFChunkSequentialReader(_db, _db.Config.WriterCheckpoint, 0);

            RecordReadResult res;
            int count1 = 0;
            while ((res = seqReader.TryReadNext()).Success)
            {
                var rec = _keptRecords[count1];
                Assert.AreEqual(rec, res.LogRecord);
                //Assert.AreEqual(rec.Position + rec.GetSizeWithLengthPrefixAndSuffix(), seqReader.Position);

                ++count1;
            }
            Assert.AreEqual(_keptRecords.Length, count1);

            int count2 = 0;
            while ((res = seqReader.TryReadPrev()).Success)
            {
                var rec = _keptRecords[_keptRecords.Length - count2 - 1];
                Assert.AreEqual(rec, res.LogRecord);
                //Assert.AreEqual(rec.Position, seqReader.Position);

                ++count2;
            }
            Assert.AreEqual(_keptRecords.Length, count2);
        }

        [Test]
        public void records_can_be_read_forward_starting_from_any_position()
        {
            for (int i = 0; i < RecordsCount; ++i)
            {
                var seqReader = new TFChunkSequentialReader(_db, _db.Config.WriterCheckpoint, _records[i].Position);

                RecordReadResult res;
                int count = 0;
                while ((res = seqReader.TryReadNext()).Success)
                {
                    var rec = _keptRecords[(i+1)/2 + count];
                    Assert.AreEqual(rec, res.LogRecord);
                    //Assert.AreEqual(rec.Position + rec.GetSizeWithLengthPrefixAndSuffix(), seqReader.Position);

                    ++count;
                }
                Assert.AreEqual(_keptRecords.Length - (i+1)/2, count);
            }
        }

        [Test]
        public void records_can_be_read_backwards_starting_from_any_position()
        {
            for (int i = 0; i < RecordsCount; ++i)
            {
                var seqReader = new TFChunkSequentialReader(_db, _db.Config.WriterCheckpoint, _records[i].Position);

                RecordReadResult res;
                int count = 0;
                while ((res = seqReader.TryReadPrev()).Success)
                {
                    var rec = _keptRecords[(i+1)/2 - count - 1];
                    Assert.AreEqual(rec, res.LogRecord);
                    Assert.AreEqual(rec.Position, seqReader.Position);

                    ++count;
                }
                Assert.AreEqual((i+1)/2, count);
            }
        }
    }
}