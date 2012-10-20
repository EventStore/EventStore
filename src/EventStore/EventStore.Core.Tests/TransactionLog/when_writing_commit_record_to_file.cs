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
/*using System;
using System.IO;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_writing_commit_record_to_file
    {
        private string _tempFileName;
        private TransactionFileWriter _transactionFileWriter;
        private InMemoryCheckpoint _checkSum;
        private readonly Guid _eventId = Guid.NewGuid();
        private CommitLogRecord _record;

        [SetUp]
        public void SetUp()
        {
            _tempFileName = Path.GetTempFileName();
            _checkSum = new InMemoryCheckpoint();
            _transactionFileWriter = new TransactionFileWriter(_tempFileName, _checkSum);
            _transactionFileWriter.Open();
            _record = new CommitLogRecord(logPosition: 0xFEED,
                                          correlationId: _eventId,
                                          startPosition: 4321,
                                          timeStamp: new DateTime(2012, 12, 21),
                                          eventNumber: 10);
            _transactionFileWriter.Write(_record);
            _transactionFileWriter.Flush();
        }

        [Test]
        public void the_data_is_written()
        {
            //TODO MAKE THIS ACTUALLY ASSERT OFF THE FILE AND READER FROM KNOWN FILE
            using (var reader = new TransactionFileReader(_tempFileName, _checkSum))
            {
                reader.Open();
                LogRecord r = null;
                Assert.IsTrue(reader.TryReadRecord(ref r));

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
            Assert.AreEqual(_record.GetSizeWithLengthPrefix(), _checkSum.Read());
        }

        [Test]
        public void trying_to_read_past_writer_checksum_returns_false()
        {
            using (var reader = new TransactionFileReader(_tempFileName, new InMemoryCheckpoint(0)))
            {
                LogRecord record = null;
                reader.Open();
                Assert.IsFalse(reader.TryReadRecordAt(ref record, 5));
            }
        }

        [TearDown]
        public void Teardown()
        {
            _transactionFileWriter.Close();
        }
    }
}*/