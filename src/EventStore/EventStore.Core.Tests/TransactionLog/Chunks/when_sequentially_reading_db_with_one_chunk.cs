// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  
using System;
using EventStore.Core.Data;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_sequentially_reading_db_with_one_chunk: SpecificationWithDirectoryPerTestFixture
    {
        private const int RecordsCount = 3;

        private TFChunkDb _db;
        private LogRecord[] _records;
        private RecordWriteResult[] _results;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            ICheckpoint[] namedCheckpoints = new ICheckpoint[0];
            ICheckpoint truncateCheckpoint = new InMemoryCheckpoint(-1);
            _db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                    new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                    4096,
                                                    0,
                                                    new InMemoryCheckpoint(),
                                                    new InMemoryCheckpoint(),
                                                    new InMemoryCheckpoint(-1),
                                                    new InMemoryCheckpoint(-1)));
            _db.Open();
            
            var chunk = _db.Manager.GetChunk(0);

            _records = new LogRecord[RecordsCount];
            _results = new RecordWriteResult[RecordsCount];

            for (int i = 0; i < _records.Length; ++i)
            {
                _records[i] = LogRecord.SingleWrite(i == 0 ? 0 : _results[i - 1].NewPosition,
                                                    Guid.NewGuid(), Guid.NewGuid(), "es1", ExpectedVersion.Any, "et1",
                                                    new byte[] { 0, 1, 2 }, new byte[] { 5, 7 });
                _results[i] = chunk.TryAppend(_records[i]);
            }

            chunk.Flush();
            _db.Config.WriterCheckpoint.Write(_results[RecordsCount - 1].NewPosition);
            _db.Config.WriterCheckpoint.Flush();
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
                Assert.IsTrue(_results[i].Success);
                Assert.AreEqual(pos, _results[i].OldPosition);

                pos += _records[i].GetSizeWithLengthPrefixAndSuffix();
                Assert.AreEqual(pos, _results[i].NewPosition);
            }
        }

        [Test]
        public void all_records_could_be_read_with_forward_pass()
        {
            var seqReader = new TFChunkReader(_db, _db.Config.WriterCheckpoint, 0);

            SeqReadResult res;
            int count = 0;
            while ((res = seqReader.TryReadNext()).Success)
            {
                var rec = _records[count];
                Assert.AreEqual(rec, res.LogRecord);
                Assert.AreEqual(rec.Position, res.RecordPrePosition);
                Assert.AreEqual(rec.Position + rec.GetSizeWithLengthPrefixAndSuffix(), res.RecordPostPosition);

                ++count;
            }
            Assert.AreEqual(RecordsCount, count);
        }

        [Test]
        public void all_records_could_be_read_with_backward_pass()
        {
            var seqReader = new TFChunkReader(_db, _db.Config.WriterCheckpoint, _db.Config.WriterCheckpoint.Read());

            SeqReadResult res;
            int count = 0;
            while ((res = seqReader.TryReadPrev()).Success)
            {
                var rec = _records[RecordsCount - count - 1];
                Assert.AreEqual(rec, res.LogRecord);
                Assert.AreEqual(rec.Position, res.RecordPrePosition);
                Assert.AreEqual(rec.Position + rec.GetSizeWithLengthPrefixAndSuffix(), res.RecordPostPosition);

                ++count;
            }
            Assert.AreEqual(RecordsCount, count);
        }

        [Test]
        public void all_records_could_be_read_doing_forward_backward_pass()
        {
            var seqReader = new TFChunkReader(_db, _db.Config.WriterCheckpoint, 0);

            SeqReadResult res;
            int count1 = 0;
            while ((res = seqReader.TryReadNext()).Success)
            {
                var rec = _records[count1];
                Assert.AreEqual(rec, res.LogRecord);
                Assert.AreEqual(rec.Position, res.RecordPrePosition);
                Assert.AreEqual(rec.Position + rec.GetSizeWithLengthPrefixAndSuffix(), res.RecordPostPosition);

                ++count1;
            }
            Assert.AreEqual(RecordsCount, count1);

            int count2 = 0;
            while ((res = seqReader.TryReadPrev()).Success)
            {
                var rec = _records[RecordsCount - count2 - 1];
                Assert.AreEqual(rec, res.LogRecord);
                Assert.AreEqual(rec.Position, res.RecordPrePosition);
                Assert.AreEqual(rec.Position + rec.GetSizeWithLengthPrefixAndSuffix(), res.RecordPostPosition);

                ++count2;
            }
            Assert.AreEqual(RecordsCount, count2);
        }

        [Test]
        public void records_can_be_read_forward_starting_from_any_position()
        {
            for (int i = 0; i < RecordsCount; ++i)
            {
                var seqReader = new TFChunkReader(_db, _db.Config.WriterCheckpoint, _records[i].Position);

                SeqReadResult res;
                int count = 0;
                while ((res = seqReader.TryReadNext()).Success)
                {
                    var rec = _records[i + count];
                    Assert.AreEqual(rec, res.LogRecord);
                    Assert.AreEqual(rec.Position, res.RecordPrePosition);
                    Assert.AreEqual(rec.Position + rec.GetSizeWithLengthPrefixAndSuffix(), res.RecordPostPosition);

                    ++count;
                }
                Assert.AreEqual(RecordsCount - i, count);
            }
        }

        [Test]
        public void records_can_be_read_backward_starting_from_any_position()
        {
            for (int i = 0; i < RecordsCount; ++i)
            {
                var seqReader = new TFChunkReader(_db, _db.Config.WriterCheckpoint, _records[i].Position);

                SeqReadResult res;
                int count = 0;
                while ((res = seqReader.TryReadPrev()).Success)
                {
                    var rec = _records[i - count - 1];
                    Assert.AreEqual(rec, res.LogRecord);
                    Assert.AreEqual(rec.Position, res.RecordPrePosition);
                    Assert.AreEqual(rec.Position + rec.GetSizeWithLengthPrefixAndSuffix(), res.RecordPostPosition);

                    ++count;
                }
                Assert.AreEqual(i, count);
            }
        }
    }
}