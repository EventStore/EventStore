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
using System.Threading;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Transactions
{
    [TestFixture, Ignore]
    public class when_rebuilding_index_for_partially_persisted_transaction : ReadIndexTestScenario
    {
        private const int MaxEntriesInMemTable = 10;

        public when_rebuilding_index_for_partially_persisted_transaction(): base(maxEntriesInMemTable: MaxEntriesInMemTable)
        {
        }

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            ReadIndex.Close();
            ReadIndex.Dispose();
            TableIndex.ClearAll(removeFiles: false);

            TableIndex = new TableIndex(Path.Combine(PathName, "index"),
                                        () => new HashListMemTable(maxSize: 2000),
                                        maxSizeForMemory: MaxEntriesInMemTable);
            TableIndex.Initialize();

            ReadIndex = new ReadIndex(new NoopPublisher(),
                                      2,
                                      () => new TFChunkSequentialReader(Db, WriterCheckpoint, 0),
                                      () => new TFChunkReader(Db, WriterCheckpoint),
                                      TableIndex,
                                      new ByLengthHasher(),
                                      new NoLRUCache<string, StreamMetadata>());
            ReadIndex.Build();
        }

        public override void TestFixtureTearDown()
        {
            Thread.Sleep(500); // give chance to IndexMap to dump files
            base.TestFixtureTearDown();
        }

        protected override void WriteTestScenario()
        {
            var begin = WriteTransactionBegin("ES", ExpectedVersion.Any);
            for (int i = 0; i < 15; ++i)
            {
                WriteTransactionEvent(Guid.NewGuid(), begin.LogPosition, i, "ES", i, "data" + i, PrepareFlags.Data);
            }
            WriteTransactionEnd(Guid.NewGuid(), begin.LogPosition, "ES");
            WriteCommit(Guid.NewGuid(), begin.LogPosition, "ES", 0);
        }

        [Test]
        public void sequence_numbers_are_not_broken()
        {
            for (int i = 0; i < 15; ++i)
            {
                EventRecord record;
                Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", i, out record));
                Assert.AreEqual(Encoding.UTF8.GetBytes("data" + i), record.Data);
            }
        }
    }
}