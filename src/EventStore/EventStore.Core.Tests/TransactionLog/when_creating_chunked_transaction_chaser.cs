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
    public class when_creating_chunked_transaction_chaser: SpecificationWithDirectory
    {
        [Test]
        public void a_null_file_config_throws_argument_null_exception()
        {
            Assert.Throws<ArgumentNullException>(
                () => new TFChunkChaser(null, new InMemoryCheckpoint(0), new InMemoryCheckpoint(0)));
        }

        [Test]
        public void a_null_writer_checksum_throws_argument_null_exception()
        {
            var db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                       new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                       10000,
                                                       0,
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(-1),
                                                       new InMemoryCheckpoint(-1)));
            Assert.Throws<ArgumentNullException>(() => new TFChunkChaser(db, null, new InMemoryCheckpoint()));
        }

        [Test]
        public void a_null_chaser_checksum_throws_argument_null_exception()
        {
            var db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                       new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                       10000,
                                                       0,
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(-1),
                                                       new InMemoryCheckpoint(-1)));
            Assert.Throws<ArgumentNullException>(() => new TFChunkChaser(db, new InMemoryCheckpoint(), null));
        }
    }
}