using System;
using System.IO;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_creating_chunked_transaction_file_reader: SpecificationWithDirectory
    {
        [Test]
        public void a_null_db_config_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(() => new TFChunkReader(null, new InMemoryCheckpoint(0)));
        }

        [Test]
        public void a_null_checkpoint_throws_argument_null_exception()
        {
            var config = new TFChunkDbConfig(PathName,
                                             new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                             10000,
                                             0,
                                             new InMemoryCheckpoint(0),
                                             new InMemoryCheckpoint(0),
                                             new InMemoryCheckpoint(-1),
                                             new InMemoryCheckpoint(-1));
            var db = new TFChunkDb(config);
            Assert.Throws<ArgumentNullException>(() => new TFChunkReader(db, null));
        }
    }
}