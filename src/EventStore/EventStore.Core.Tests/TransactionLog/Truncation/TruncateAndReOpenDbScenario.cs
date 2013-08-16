using System.IO;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace EventStore.Core.Tests.TransactionLog.Truncation
{
    public abstract class TruncateAndReOpenDbScenario : TruncateScenario
    {
        protected TruncateAndReOpenDbScenario(int maxEntriesInMemTable = 100, int metastreamMaxCount = 1)
            : base(maxEntriesInMemTable, metastreamMaxCount)
        {
        }

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            ReOpenDb();
        }

        private void ReOpenDb()
        {
            Db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                   new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                   10000,
                                                   0,
                                                   WriterCheckpoint,
                                                   ChaserCheckpoint,
                                                   new InMemoryCheckpoint(-1),
                                                   new InMemoryCheckpoint(-1)));

            Db.Open();

            var readers = new ObjectPool<ITransactionFileReader>("Readers", 2, 5, () => new TFChunkReader(Db, Db.Config.WriterCheckpoint));
            TableIndex = new TableIndex(Path.Combine(PathName, "index"),
                                        () => new HashListMemTable(MaxEntriesInMemTable * 2),
                                        () => new TFReaderLease(readers),
                                        MaxEntriesInMemTable);
            ReadIndex = new ReadIndex(new NoopPublisher(),
                                      readers,
                                      TableIndex,
                                      new ByLengthHasher(),
                                      new NoLRUCache<string, StreamCacheInfo>(),
                                      additionalCommitChecks: true,
                                      metastreamMaxCount: MetastreamMaxCount);
            ReadIndex.Init(ChaserCheckpoint.Read());
        }
    }
}