using System;
using System.IO;
using System.Text;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage
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

            Thread.Sleep(500);
            TableIndex.ClearAll(removeFiles: false);

            TableIndex = new TableIndex(Path.Combine(PathName, "index"), () => new HashListMemTable(), maxSizeForMemory: 5);
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
            try
            {
                base.TestFixtureTearDown();
            }
            catch
            {
                // TODO AN this is VERY bad, but it fails only on CI on Windows, not priority to check who holds lock on file
            }
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