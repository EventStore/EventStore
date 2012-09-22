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
    [TestFixture]
    public class when_building_an_index_off_tfile_with_prepares_but_no_commits : SpecificationWithDirectory
    {
        private IReadIndex _idx;

        [SetUp]
        public void Setup()
        {
            var writerchk = new InMemoryCheckpoint(0);
            var chaserchk = new InMemoryCheckpoint(Checkpoint.Chaser, 0);
            var config = new TransactionFileDatabaseConfig(PathName, "prefix.tf", 10000, writerchk, new[] {chaserchk});
            // create db
            var writer = new MultifileTransactionFileWriter(config);
            writer.Open();
            long p1;
            writer.Write(new PrepareLogRecord(0, Guid.NewGuid(), Guid.NewGuid(), 0, "test1", -1, DateTime.UtcNow,
                                              PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
                         out p1);
            long p2;
             writer.Write(new PrepareLogRecord(p1, Guid.NewGuid(), Guid.NewGuid(), p1, "test2", -1, DateTime.UtcNow,
                                               PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]),
                          out p2);
            long p3;
            writer.Write(new PrepareLogRecord(p2, Guid.NewGuid(), Guid.NewGuid(), p2, "test3", -1, DateTime.UtcNow,
                                              PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]), 
                         out p3);
            writer.Close();

            chaserchk.Write(writerchk.Read());
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
        public void the_first_stream_is_not_in_index_yet()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, _idx.TryReadRecord("test1", 0, out record));
        }

        [Test]
        public void the_second_stream_is_not_in_index_yet()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NoStream, _idx.TryReadRecord("test2", 0, out record));
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