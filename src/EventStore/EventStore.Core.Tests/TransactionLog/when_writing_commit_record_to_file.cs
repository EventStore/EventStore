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
using System.IO;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_writing_commit_record_to_file: SpecificationWithDirectoryPerTestFixture
    {
        private ITransactionFileWriter _writer;
        private InMemoryCheckpoint _writerCheckpoint;
        private readonly Guid _eventId = Guid.NewGuid();
        private CommitLogRecord _record;
        private TFChunkDb _db;

        [TestFixtureSetUp]
        public void SetUp()
        {
            _writerCheckpoint = new InMemoryCheckpoint();
            _db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                    new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                    1024,
                                                    0,
                                                    _writerCheckpoint,
                                                    new InMemoryCheckpoint(),
                                                    new ICheckpoint[0]));
            _db.OpenVerifyAndClean();
            _writer = new TFChunkWriter(_db);
            _writer.Open();
            _record = new CommitLogRecord(logPosition: 0xFEED,
                                          correlationId: _eventId,
                                          transactionPosition: 4321,
                                          timeStamp: new DateTime(2012, 12, 21),
                                          eventNumber: 10);
            long newPos;
            _writer.Write(_record, out newPos);
            _writer.Flush();
        }

        [TestFixtureTearDown]
        public void Teardown()
        {
            _writer.Close();
            _db.Close();
        }

        [Test]
        public void the_data_is_written()
        {
            using (var reader = new TFChunkChaser(_db, _writerCheckpoint, _db.Config.ChaserCheckpoint))
            {
                reader.Open();
                LogRecord r;
                Assert.IsTrue(reader.TryReadNext(out r));

                Assert.True(r is CommitLogRecord);
                var c = (CommitLogRecord) r;
                Assert.AreEqual(c.RecordType, LogRecordType.Commit);
                Assert.AreEqual(c.LogPosition, 0xFEED);
                Assert.AreEqual(c.CorrelationId, _eventId);
                Assert.AreEqual(c.TransactionPosition, 4321);
                Assert.AreEqual(c.TimeStamp, new DateTime(2012, 12, 21));
            }
        }

        [Test]
        public void the_checksum_is_updated()
        {
            Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), _writerCheckpoint.Read());
        }

        [Test]
        public void trying_to_read_past_writer_checksum_returns_false()
        {
            using (var reader = new TFChunkReader(_db, _writerCheckpoint))
            {
                reader.Open();
                Assert.IsFalse(reader.TryReadAt(_writerCheckpoint.Read()).Success);
            }
        }
    }
}