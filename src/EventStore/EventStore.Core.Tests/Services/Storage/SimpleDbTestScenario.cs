using EventStore.Common.Utils;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage
{
    [TestFixture]
    public abstract class SimpleDbTestScenario: SpecificationWithDirectoryPerTestFixture
    {
        protected readonly int MaxEntriesInMemTable;
        protected TableIndex TableIndex;
        protected IReadIndex ReadIndex;

        protected DbResult DbRes;

        protected abstract DbResult CreateDb(TFChunkDbCreationHelper dbCreator);

        private readonly int _metastreamMaxCount;

        protected SimpleDbTestScenario(int maxEntriesInMemTable = 20, int metastreamMaxCount = 1)
        {
            Ensure.Positive(maxEntriesInMemTable, "maxEntriesInMemTable");
            MaxEntriesInMemTable = maxEntriesInMemTable;
            _metastreamMaxCount = metastreamMaxCount;
        }

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            var dbConfig = new TFChunkDbConfig(PathName,
                                               new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                               1024*1024,
                                               0,
                                               new InMemoryCheckpoint(0),
                                               new InMemoryCheckpoint(0),
                                               new InMemoryCheckpoint(-1),
                                               new InMemoryCheckpoint(-1));
            var dbCreationHelper = new TFChunkDbCreationHelper(dbConfig);
            
            DbRes = CreateDb(dbCreationHelper);

            DbRes.Db.Config.WriterCheckpoint.Flush();
            DbRes.Db.Config.ChaserCheckpoint.Write(DbRes.Db.Config.WriterCheckpoint.Read());
            DbRes.Db.Config.ChaserCheckpoint.Flush();

            var readers = new ObjectPool<ITransactionFileReader>(
                "Readers", 2, 2, () => new TFChunkReader(DbRes.Db, DbRes.Db.Config.WriterCheckpoint));

            TableIndex = new TableIndex(GetFilePathFor("index"),
                                        () => new HashListMemTable(MaxEntriesInMemTable * 2),
                                        () => new TFReaderLease(readers),
                                        MaxEntriesInMemTable);

            ReadIndex = new ReadIndex(new NoopPublisher(),
                                      readers,
                                      TableIndex,
                                      new ByLengthHasher(),
                                      new NoLRUCache<string, StreamCacheInfo>(),
                                      additionalCommitChecks: true,
                                      metastreamMaxCount: _metastreamMaxCount);

            ReadIndex.Init(DbRes.Db.Config.ChaserCheckpoint.Read());
        }

        public override void TestFixtureTearDown()
        {
            DbRes.Db.Close();

            base.TestFixtureTearDown();
        }
    }
}
