﻿using System.IO;
using EventStore.Core.DataStructures;
using EventStore.Core.Index;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.Util;
using EventStore.Core.Index.Hashes;

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
            var lowHasher = new XXHashUnsafe();
            var highHasher = new Murmur3AUnsafe();
            TableIndex = new TableIndex(Path.Combine(PathName, "index"), lowHasher, highHasher,
                                        () => new HashListMemTable(PTableVersions.IndexV3, MaxEntriesInMemTable * 2),
                                        () => new TFReaderLease(readers),
                                        PTableVersions.IndexV3,
                                        MaxEntriesInMemTable);
            ReadIndex = new ReadIndex(new NoopPublisher(),
                                      readers,
                                      TableIndex,
                                      0,
                                      additionalCommitChecks: true,
                                      metastreamMaxCount: MetastreamMaxCount,
                                      hashCollisionReadLimit: Opts.HashCollisionReadLimitDefault);
            ReadIndex.Init(ChaserCheckpoint.Read());
        }
    }
}