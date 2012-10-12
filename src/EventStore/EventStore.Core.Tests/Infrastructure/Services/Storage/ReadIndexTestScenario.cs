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
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage
{
    public abstract class ReadIndexTestScenario: SpecificationWithDirectoryPerTestFixture
    {
        private readonly int _maxEntriesInMemTable;
        protected TableIndex TableIndex;
        protected ReadIndex ReadIndex;

        protected TFChunkDb Db;
        protected TFChunkWriter Writer;
        protected ICheckpoint WriterCheckpoint;

        protected ReadIndexTestScenario(int maxEntriesInMemTable = 1000000)
        {
            Ensure.Positive(maxEntriesInMemTable, "maxEntriesInMemTable");
            _maxEntriesInMemTable = maxEntriesInMemTable;
        }

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            WriterCheckpoint = new InMemoryCheckpoint(0);
            var chaserchk = new InMemoryCheckpoint(Checkpoint.Chaser, 0);
            Db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                   new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                   10000,
                                                   0,
                                                   WriterCheckpoint,
                                                   new[] {chaserchk}));
            Db.OpenVerifyAndClean();
            // create db
            Writer = new TFChunkWriter(Db);
            Writer.Open();
            WriteTestScenario();
            Writer.Close();
            Writer = null;

            WriterCheckpoint.Flush();
            chaserchk.Write(WriterCheckpoint.Read());
            chaserchk.Flush();

            TableIndex = new TableIndex(Path.Combine(PathName, "index"), () => new HashListMemTable(), _maxEntriesInMemTable);
            TableIndex.Initialize();

            var reader = new TFChunkReader(Db, Db.Config.WriterCheckpoint);
            ReadIndex = new ReadIndex(new NoopPublisher(),
                                      () => new TFChunkSequentialReader(Db, Db.Config.WriterCheckpoint, 0), 
                                      2,
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

            Db.Close();
            Db.Dispose();

            base.TestFixtureTearDown();
        }

        protected abstract void WriteTestScenario();

        protected EventRecord WriteSingleEvent(string eventStreamId, int eventNumber, string data)
        {
            var prepare = LogRecord.SingleWrite(WriterCheckpoint.ReadNonFlushed(),
                                                Guid.NewGuid(),
                                                Guid.NewGuid(),
                                                eventStreamId,
                                                eventNumber - 1,
                                                "some-type",
                                                Encoding.UTF8.GetBytes(data),
                                                null);
            long pos;
            Assert.IsTrue(Writer.Write(prepare, out pos));

            var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), prepare.CorrelationId, prepare.LogPosition, eventNumber);
            Assert.IsTrue(Writer.Write(commit, out pos));

            var eventRecord = new EventRecord(eventNumber, prepare);
            return eventRecord;
        }

        protected EventRecord WriteTransactionBegin(string eventStreamId, int expectedVersion, int eventNumber, string eventData)
        {
            var prepare = LogRecord.Prepare(WriterCheckpoint.ReadNonFlushed(),
                                            Guid.NewGuid(),
                                            Guid.NewGuid(),
                                            WriterCheckpoint.ReadNonFlushed(),
                                            0,
                                            eventStreamId,
                                            expectedVersion,
                                            PrepareFlags.Data | PrepareFlags.TransactionBegin, 
                                            "some-type",
                                            Encoding.UTF8.GetBytes(eventData),
                                            null);
            long pos;
            Assert.IsTrue(Writer.Write(prepare, out pos));
            return new EventRecord(eventNumber, prepare);
        }

        protected PrepareLogRecord WriteTransactionBegin(string eventStreamId, int expectedVersion)
        {
            var prepare = LogRecord.TransactionBegin(WriterCheckpoint.ReadNonFlushed(), Guid.NewGuid(), eventStreamId, expectedVersion);
            long pos;
            Assert.IsTrue(Writer.Write(prepare, out pos));
            return prepare;
        }

        protected EventRecord WriteTransactionEvent(Guid correlationId, 
                                                    long transactionPos, 
                                                    int transactionOffset,
                                                    string eventStreamId, 
                                                    int eventNumber, 
                                                    string eventData,
                                                    PrepareFlags flags)
        {
            var prepare = LogRecord.Prepare(WriterCheckpoint.ReadNonFlushed(),
                                            correlationId,
                                            Guid.NewGuid(),
                                            transactionPos,
                                            transactionOffset,
                                            eventStreamId,
                                            ExpectedVersion.Any,
                                            flags,
                                            "some-type",
                                            Encoding.UTF8.GetBytes(eventData),
                                            null);
            long pos;
            Assert.IsTrue(Writer.Write(prepare, out pos));
            return new EventRecord(eventNumber, prepare);
        }

        protected PrepareLogRecord WriteTransactionEnd(Guid correlationId, long transactionId, string eventStreamId)
        {
            var prepare = LogRecord.TransactionEnd(WriterCheckpoint.ReadNonFlushed(),
                                                   correlationId,
                                                   Guid.NewGuid(),
                                                   transactionId,
                                                   eventStreamId);
            long pos;
            Assert.IsTrue(Writer.Write(prepare, out pos));
            return prepare;
        }

        protected long WriteCommit(Guid correlationId, long transactionId, string eventStreamId, int eventNumber)
        {
            var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(), correlationId, transactionId, eventNumber);
            long pos;
            Assert.IsTrue(Writer.Write(commit, out pos));
            return commit.LogPosition;
        }

        protected void WriteDelete(string eventStreamId)
        {
            var prepare = LogRecord.DeleteTombstone(WriterCheckpoint.ReadNonFlushed(),
                                                           Guid.NewGuid(),
                                                           eventStreamId,
                                                           ExpectedVersion.Any);
            long pos;
            Assert.IsTrue(Writer.Write(prepare, out pos));
            var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(),
                                          prepare.CorrelationId,
                                          prepare.LogPosition,
                                          EventNumber.DeletedStream);
            Assert.IsTrue(Writer.Write(commit, out pos));
        }
    }
}