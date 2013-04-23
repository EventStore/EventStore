using System.IO;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Storage;
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

            TableIndex = new TableIndex(Path.Combine(PathName, "index"),
                                        () => new HashListMemTable(MaxEntriesInMemTable * 2),
                                        MaxEntriesInMemTable);
            ReadIndex = new ReadIndex(new NoopPublisher(),
                                      2,
                                      5,
                                      () => new TFChunkReader(Db, Db.Config.WriterCheckpoint),
                                      TableIndex,
                                      new ByLengthHasher(),
                                      new NoLRUCache<string, StreamCacheInfo>(),
                                      additionalCommitChecks: true,
                                      metastreamMaxCount: MetastreamMaxCount);
            ReadIndex.Init(WriterCheckpoint.Read(), ChaserCheckpoint.Read());
        }
    }
}