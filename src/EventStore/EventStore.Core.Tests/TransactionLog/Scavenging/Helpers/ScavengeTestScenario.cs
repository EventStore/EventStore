using System;
using System.Collections.Generic;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers
{
    [TestFixture]
    public abstract class ScavengeTestScenario: SpecificationWithDirectoryPerTestFixture
    {
        private DbResult _dbResult;
        private LogRecord[][] _keptRecords;
        private bool _checked;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            var dbConfig = new TFChunkDbConfig(PathName,
                                               new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                               1024*1024,
                                               0,
                                               new InMemoryCheckpoint(0),
                                               new InMemoryCheckpoint(0));
            var dbCreationHelper = new TFChunkDbCreationHelper(dbConfig);
            _dbResult = CreateDb(dbCreationHelper);
            _keptRecords = KeptRecords(_dbResult);

            var scavengeReadIndex = new ScavengeReadIndex(_dbResult.Streams);
            var scavenger = new TFChunkScavenger(_dbResult.Db, scavengeReadIndex);
            scavenger.Scavenge(alwaysKeepScavenged: true);
        }

        public override void TestFixtureTearDown()
        {
            _dbResult.Db.Close();

            base.TestFixtureTearDown();

            if (!_checked)
                throw new Exception("Records weren't checked. Probably you forgot to call CheckRecords() method.");
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
    }
}
