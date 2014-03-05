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

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_sequentially_reading_db_with_one_chunk_ending_with_prepare :
        SpecificationWithDirectoryPerTestFixture
    {
        private const int RecordsCount = 3;

        private TFChunkDb _db;
        private LogRecord[] _records;
        private RecordWriteResult[] _results;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _db =
                new TFChunkDb(
                    new TFChunkDbConfig(
                        PathName,
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

            for (int i = 0; i < _records.Length - 1; ++i)
            {
                _records[i] = LogRecord.SingleWrite(
                    i == 0 ? 0 : _results[i - 1].NewPosition,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "es1",
                    ExpectedVersion.Any,
                    "et1",
                    new byte[] {0, 1, 2},
                    new byte[] {5, 7});
                _results[i] = chunk.TryAppend(_records[i]);
            }
            _records[_records.Length -1] = LogRecord.Prepare(
                _results[_records.Length - 1 - 1].NewPosition,
                Guid.NewGuid(),
                Guid.NewGuid(),
                _results[_records.Length - 1 - 1].NewPosition,
                0,
                "es1",
                ExpectedVersion.Any,
                PrepareFlags.Data,
                "et1",
                new byte[] { 0, 1, 2 },
                new byte[] { 5, 7 });
            _results[_records.Length - 1] = chunk.TryAppend(_records[_records.Length - 1]);

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
        public void only_the_last_record_is_marked_eof()
        {
            var seqReader = new TFChunkReader(_db, _db.Config.WriterCheckpoint, 0);

            SeqReadResult res;
            int count = 0;
            while ((res = seqReader.TryReadNext()).Success)
            {
                ++count;
                Assert.AreEqual(count == RecordsCount, res.Eof);
            }
            Assert.AreEqual(RecordsCount, count);
        }

    }
}
