using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EventStore.Core.Bus;
using EventStore.Core.DataStructures;
using EventStore.Core.Helpers;
using EventStore.Core.Index;
using EventStore.Core.Index.Hashes;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Settings;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using EventStore.Core.Util;

using System.Text;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers
{
    [TestFixture]
    public abstract class ScavengeTestScenario: SpecificationWithDirectoryPerTestFixture
    {
        protected IReadIndex ReadIndex;
        protected TFChunkDb Db { get { return _dbResult.Db; } }

        private readonly int _metastreamMaxCount;
        private DbResult _dbResult;
        private LogRecord[][] _keptRecords;
        private bool _checked;

        protected virtual bool UnsafeIgnoreHardDelete() {
            return false;
        }

        protected ScavengeTestScenario(int metastreamMaxCount = 1)
        {
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
            _dbResult = CreateDb(dbCreationHelper);
            _keptRecords = KeptRecords(_dbResult);

            _dbResult.Db.Config.WriterCheckpoint.Flush();
            _dbResult.Db.Config.ChaserCheckpoint.Write(_dbResult.Db.Config.WriterCheckpoint.Read());
            _dbResult.Db.Config.ChaserCheckpoint.Flush();

            var indexPath = Path.Combine(PathName, "index");
            var readerPool = new ObjectPool<ITransactionFileReader>(
                "ReadIndex readers pool", ESConsts.PTableInitialReaderCount, ESConsts.PTableMaxReaderCount,
                () => new TFChunkReader(_dbResult.Db, _dbResult.Db.Config.WriterCheckpoint));
            var lowHasher = new XXHashUnsafe();
            var highHasher = new Murmur3AUnsafe();
            var tableIndex = new TableIndex(indexPath, lowHasher, highHasher,
                                            () => new HashListMemTable(PTableVersions.IndexV3, maxSize: 200),
                                            () => new TFReaderLease(readerPool),
                                            PTableVersions.IndexV3,
                                            maxSizeForMemory: 100,
                                            maxTablesPerLevel: 2);
            ReadIndex = new ReadIndex(new NoopPublisher(), readerPool, tableIndex, 100, true, _metastreamMaxCount, Opts.HashCollisionReadLimitDefault);
            ReadIndex.Init(_dbResult.Db.Config.WriterCheckpoint.Read());

            //var scavengeReadIndex = new ScavengeReadIndex(_dbResult.Streams, _metastreamMaxCount);
            var bus = new InMemoryBus("Bus");
            var ioDispatcher = new IODispatcher(bus, new PublishEnvelope(bus));
            var scavenger = new TFChunkScavenger(_dbResult.Db, ioDispatcher, tableIndex, ReadIndex, Guid.NewGuid(), "fakeNodeIp",
                                            unsafeIgnoreHardDeletes: UnsafeIgnoreHardDelete());
            scavenger.Scavenge(alwaysKeepScavenged: true, mergeChunks: false);
        }

        public override void TestFixtureTearDown()
        {
            ReadIndex.Close();
            _dbResult.Db.Close();

            base.TestFixtureTearDown();

            if (!_checked)
                throw new Exception("Records were not checked. Probably you forgot to call CheckRecords() method.");
        }

        protected abstract DbResult CreateDb(TFChunkDbCreationHelper dbCreator);

        protected abstract LogRecord[][] KeptRecords(DbResult dbResult);

        protected void CheckRecords()
        {
            _checked = true;
            Assert.AreEqual(_keptRecords.Length, _dbResult.Db.Manager.ChunksCount, "Wrong chunks count.");

            for (int i = 0; i < _keptRecords.Length; ++i)
            {
                var chunk = _dbResult.Db.Manager.GetChunk(i);

                var chunkRecords = new List<LogRecord>();
                RecordReadResult result = chunk.TryReadFirst();
                while (result.Success)
                {
                    chunkRecords.Add(result.LogRecord);
                    result = chunk.TryReadClosestForward((int)result.NextPosition);
                }

                Assert.AreEqual(_keptRecords[i].Length, chunkRecords.Count, "Wrong number of records in chunk #{0}", i);

                for (int j = 0; j < _keptRecords[i].Length; ++j)
                {
                    Assert.AreEqual(_keptRecords[i][j], chunkRecords[j], "Wrong log record #{0} read from chunk #{1}", j, i);
                }
            }
        }

        protected void CheckRecordsV0()
        {
            _checked = true;
            Assert.AreEqual(_keptRecords.Length, _dbResult.Db.Manager.ChunksCount, "Wrong chunks count.");

            for (int i = 0; i < _keptRecords.Length; ++i)
            {
                var chunk = _dbResult.Db.Manager.GetChunk(i);

                var chunkRecords = new List<LogRecord>();
                RecordReadResult result = chunk.TryReadFirst();
                while (result.Success)
                {
                    chunkRecords.Add(result.LogRecord);
                    result = chunk.TryReadClosestForward(result.NextPosition);
                }

                Assert.AreEqual(_keptRecords[i].Length, chunkRecords.Count, "Wrong number of records in chunk #{0}", i);
                for (int j = 0; j < _keptRecords[i].Length; ++j)
                {
                    var keptRecord = _keptRecords[i][j];
                    var chunkRecord = chunkRecords[j];

                    Assert.AreEqual(keptRecord.RecordType, chunkRecord.RecordType, "Wrong log record #{0} read from chunk #{1}", j, i);
                    switch(keptRecord.RecordType) {
                        case LogRecordType.Prepare:
                            var keptPrepare = (PrepareLogRecord)keptRecord;
                            var chunkPrepare = (PrepareLogRecord)chunkRecord;
                            Assert.AreEqual(keptPrepare.EventId, chunkPrepare.EventId);
                            break;
                        case LogRecordType.Commit:
                            var keptCommit = (CommitLogRecord)keptRecord;
                            var chunkCommit = (CommitLogRecord)chunkRecord;
                            Assert.IsTrue(keptCommit.CorrelationId == chunkCommit.CorrelationId &&
                                          keptCommit.FirstEventNumber == chunkCommit.FirstEventNumber &&
                                          keptCommit.TransactionPosition == chunkCommit.TransactionPosition);
                            break;
                        case LogRecordType.System:
                            var keptSystem = (SystemLogRecord)keptRecord;
                            var chunkSystem = (SystemLogRecord)chunkRecord;
                            Assert.IsTrue(keptSystem.Data == chunkSystem.Data &&
                                          keptSystem.SystemRecordType == chunkSystem.SystemRecordType);
                            break;
                        default:
                            Assert.Fail("Unknown record type {0}", keptRecord.RecordType);
                            break;
                    }
                }
            }
        }
    }
}
