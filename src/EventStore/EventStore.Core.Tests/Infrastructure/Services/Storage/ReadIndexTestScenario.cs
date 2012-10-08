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
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.MultifileTransactionFile;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage
{
    public abstract class ReadIndexTestScenario: SpecificationWithDirectoryPerTestFixture
    {
        private readonly int _maxEntriesInMemTable;
        protected TableIndex TableIndex;
        protected ReadIndex ReadIndex;

        protected TransactionFileDatabaseConfig _dbConfig;
        protected MultifileTransactionFileWriter _writer;
        protected ICheckpoint _writerCheckpoint;

        protected ReadIndexTestScenario(int maxEntriesInMemTable = 1000000)
        {
            Ensure.Positive(maxEntriesInMemTable, "maxEntriesInMemTable");
            _maxEntriesInMemTable = maxEntriesInMemTable;
        }

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _writerCheckpoint = new InMemoryCheckpoint(0);
            var chaserchk = new InMemoryCheckpoint(Checkpoint.Chaser, 0);
            _dbConfig = new TransactionFileDatabaseConfig(PathName,
                                                          "prefix.tf",
                                                          10000,
                                                          _writerCheckpoint,
                                                          new[] {chaserchk});
            // create db
            _writer = new MultifileTransactionFileWriter(_dbConfig);
            _writer.Open();
            WriteTestScenario();
            _writer.Close();
            _writer = null;

            _writerCheckpoint.Flush();
            chaserchk.Write(_writerCheckpoint.Read());
            chaserchk.Flush();

            TableIndex = new TableIndex(Path.Combine(PathName, "index"),
                                        () => new HashListMemTable(),
                                        maxSizeForMemory: _maxEntriesInMemTable);
            TableIndex.Initialize();

            var reader = new MultifileTransactionFileReader(_dbConfig, _dbConfig.WriterCheckpoint);
            ReadIndex = new ReadIndex(new NoopPublisher(),
                                      pos => new MultifileTransactionFileChaser(_dbConfig, new InMemoryCheckpoint(pos)), 
                                      () => reader,
                                      1,
                                      TableIndex,
                                      new ByLengthHasher());
            ReadIndex.Build();
        }

        public override void TestFixtureTearDown()
        {
            TableIndex.ClearAll();

            ReadIndex.Close();
            ReadIndex.Dispose();

            base.TestFixtureTearDown();
        }

        protected abstract void WriteTestScenario();

        protected EventRecord WriteSingleEvent(string eventStreamId, int eventNumber, string data)
        {
            var prepare = LogRecord.SingleWrite(_writerCheckpoint.ReadNonFlushed(),
                                                Guid.NewGuid(),
                                                Guid.NewGuid(),
                                                eventStreamId,
                                                eventNumber - 1,
                                                "some-type",
                                                Encoding.UTF8.GetBytes(data),
                                                null);
            long pos;
            Assert.IsTrue(_writer.Write(prepare, out pos));

            var commit = LogRecord.Commit(_writerCheckpoint.ReadNonFlushed(), prepare.CorrelationId, prepare.LogPosition, eventNumber);
            Assert.IsTrue(_writer.Write(commit, out pos));

            var eventRecord = new EventRecord(eventNumber, prepare);
            return eventRecord;
        }

        protected EventRecord WriteTransactionBegin(string eventStreamId, int expectedVersion, int eventNumber, string eventData)
        {
            var prepare = LogRecord.Prepare(_writerCheckpoint.ReadNonFlushed(),
                                            Guid.NewGuid(),
                                            Guid.NewGuid(),
                                            _writerCheckpoint.ReadNonFlushed(),
                                            eventStreamId,
                                            expectedVersion,
                                            PrepareFlags.Data | PrepareFlags.TransactionBegin, 
                                            "some-type",
                                            Encoding.UTF8.GetBytes(eventData),
                                            null);
            long pos;
            Assert.IsTrue(_writer.Write(prepare, out pos));
            return new EventRecord(eventNumber, prepare);
        }

        protected PrepareLogRecord WriteTransactionBegin(string eventStreamId, int expectedVersion)
        {
            var prepare = LogRecord.TransactionBegin(_writerCheckpoint.ReadNonFlushed(), Guid.NewGuid(), eventStreamId, expectedVersion);
            long pos;
            Assert.IsTrue(_writer.Write(prepare, out pos));
            return prepare;
        }

        protected EventRecord WriteTransactionEvent(Guid correlationId, 
                                                    long transactionPos, 
                                                    string eventStreamId, 
                                                    int eventNumber, 
                                                    string eventData,
                                                    PrepareFlags flags)
        {
            var prepare = LogRecord.Prepare(_writerCheckpoint.ReadNonFlushed(),
                                            correlationId,
                                            Guid.NewGuid(),
                                            transactionPos,
                                            eventStreamId,
                                            ExpectedVersion.Any,
                                            flags,
                                            "some-type",
                                            Encoding.UTF8.GetBytes(eventData),
                                            null);
            long pos;
            Assert.IsTrue(_writer.Write(prepare, out pos));
            return new EventRecord(eventNumber, prepare);
        }

        protected PrepareLogRecord WriteTransactionEnd(Guid correlationId, long transactionId, string eventStreamId)
        {
            var prepare = LogRecord.TransactionEnd(_writerCheckpoint.ReadNonFlushed(),
                                                   correlationId,
                                                   Guid.NewGuid(),
                                                   transactionId,
                                                   eventStreamId);
            long pos;
            Assert.IsTrue(_writer.Write(prepare, out pos));
            return prepare;
        }

        protected void WriteCommit(Guid correlationId, long transactionId, string eventStreamId, int eventNumber)
        {
            var commit = LogRecord.Commit(_writerCheckpoint.ReadNonFlushed(), correlationId, transactionId, eventNumber);
            long pos;
            Assert.IsTrue(_writer.Write(commit, out pos));
        }

        protected void WriteDelete(string eventStreamId)
        {
            var prepare = LogRecord.DeleteTombstone(_writerCheckpoint.ReadNonFlushed(),
                                                           Guid.NewGuid(),
                                                           eventStreamId,
                                                           ExpectedVersion.Any);
            long pos;
            Assert.IsTrue(_writer.Write(prepare, out pos));
            var commit = LogRecord.Commit(_writerCheckpoint.ReadNonFlushed(),
                                          prepare.CorrelationId,
                                          prepare.LogPosition,
                                          EventNumber.DeletedStream);
            Assert.IsTrue(_writer.Write(commit, out pos));
        }
    }
}