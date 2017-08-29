using System;
using System.IO;
using EventStore.Core.Bus;
using EventStore.Core.Services.Replication;
using EventStore.Core.Tests.Services.ElectionsService;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.MasterReplication
{
    public abstract class with_master_replication_service : SpecificationWithDirectoryPerTestFixture
    {
        protected MasterReplicationService Service;
        protected TFChunkDb Db;

        protected Guid MasterId = Guid.NewGuid();
        protected int ClusterSize = 3;

        protected InMemoryBus Publisher;
        protected InMemoryBus TcpSendPublisher;

        [OneTimeSetUp]
        public void SetUp()
        {
            CreateDb();
            Publisher = new InMemoryBus("publisher");
            TcpSendPublisher = new InMemoryBus("tcpSendPublisher");
            Service = new MasterReplicationService(Publisher, MasterId, Db, TcpSendPublisher, new FakeEpochManager(), ClusterSize);

            When();
        }

        private void CreateDb()
        {
            string dbPath = Path.Combine(PathName, string.Format("mini-node-db-{0}", Guid.NewGuid()));

            var writerCheckpoint = new InMemoryCheckpoint();
            var chaserCheckpoint = new InMemoryCheckpoint();
            var replicationCheckpoint = new InMemoryCheckpoint(-1);
            Db = new TFChunkDb(new TFChunkDbConfig(dbPath,
                                                   new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
                                                   TFConsts.ChunkSize,
                                                   0,
                                                   writerCheckpoint,
                                                   chaserCheckpoint,
                                                   new InMemoryCheckpoint(-1),
                                                   new InMemoryCheckpoint(-1),
                                                   replicationCheckpoint,
                                                   inMemDb: true));

            Db.Open();
        }

        public abstract void When();
    }
}