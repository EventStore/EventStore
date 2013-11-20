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
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Transactions
{
    [TestFixture]
    public class when_rebuilding_index_for_partially_persisted_transaction : ReadIndexTestScenario
    {
        public when_rebuilding_index_for_partially_persisted_transaction(): base(maxEntriesInMemTable: 10)
        {
        }

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            ReadIndex.Close();
            ReadIndex.Dispose();
            TableIndex.Close(removeFiles: false);

            var readers = new ObjectPool<ITransactionFileReader>("Readers", 2, 2, () => new TFChunkReader(Db, WriterCheckpoint));
            TableIndex = new TableIndex(GetFilePathFor("index"),
                                        () => new HashListMemTable(maxSize: MaxEntriesInMemTable*2),
                                        () => new TFReaderLease(readers),
                                        maxSizeForMemory: MaxEntriesInMemTable);
            ReadIndex = new ReadIndex(new NoopPublisher(),
                                      readers,
                                      TableIndex,
                                      new ByLengthHasher(),
                                      0,
                                      additionalCommitChecks: true, 
                                      metastreamMaxCount: 1);
            ReadIndex.Init(ChaserCheckpoint.Read());
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
                var result = ReadIndex.ReadEvent("ES", i);
                Assert.AreEqual(ReadEventResult.Success, result.Result);
                Assert.AreEqual(Helper.UTF8NoBom.GetBytes("data" + i), result.Record.Data);
            }
        }
    }
}