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
using EventStore.Core.Data;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.TransactionLog.MultifileTransactionFile;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage
{
    [TestFixture, Ignore("ReadIndex doesn't care where chaser is, so this test is obsolete.")]
    public class when_building_an_index_off_nonempty_tfile_with_chaser_lagging_behind: SpecificationWithDirectory
    {
        private IReadIndex _idx;
        private Guid _id1;
        private Guid _id2;

        [SetUp]
        public void Setup()
        {
            var writerchk = new InMemoryCheckpoint(0);
            var chaserchk = new InMemoryCheckpoint(Checkpoint.Chaser, 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {chaserchk});
            // create db
            var writer = new MultifileTransactionFileWriter(config);
            writer.Open();
            _id1 = Guid.NewGuid();
            _id2 = Guid.NewGuid();
            long pos1, pos2, pos3, pos4;
            writer.Write(new PrepareLogRecord(0, _id1, _id1, 0, "test1", -1, DateTime.UtcNow, 
                                              PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
                         out pos1);
            writer.Write(new PrepareLogRecord(pos1, _id2, _id2, pos1, "test1", 0, DateTime.UtcNow, 
                                              PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]), 
                         out pos2);
            writer.Write(new CommitLogRecord(pos2, _id1, 0, DateTime.UtcNow, 0), out pos3);
            writer.Write(new CommitLogRecord(pos3, _id2, pos1, DateTime.UtcNow, 1), out pos4);
            writer.Close();

            chaserchk.Write(pos3); // all except last commit
            chaserchk.Flush();

            var tableIndex = new TableIndex(Path.Combine(PathName, "index"), () => new HashListMemTable(), new InMemoryCheckpoint(), 1000);
            _idx = new ReadIndex(new NoopPublisher(),
                                 pos => new MultifileTransactionFileChaser(config, new InMemoryCheckpoint(pos)),
                                 () => new MultifileTransactionFileReader(config, config.WriterCheckpoint),
                                 1,
                                 tableIndex,
                                 new XXHashUnsafe());
            _idx.Build();
        }

        [Test]
        public void the_first_event_can_be_read()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.Success, _idx.TryReadRecord("test1", 0, out record));
            Assert.AreEqual(_id1, record.EventId);
        }

        [Test]
        public void the_second_event_is_not_found()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, _idx.TryReadRecord("test1", 1, out record));
        }

        [Test]
        public void the_stream_can_be_read_with_two_events_in_right_order()
        {
            EventRecord[] records;

            Assert.AreEqual(RangeReadResult.Success, _idx.TryReadRecordsBackwards("test1", 0, 10, out records));
            Assert.AreEqual(1, records.Length);

            Assert.AreEqual(_id1, records[0].EventId);
        }

        [TearDown]
        public override void TearDown()
        {
            _idx.Close();
            _idx.Dispose();

            base.TearDown();
        }
    }
}