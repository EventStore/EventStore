using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Validation
{
    [TestFixture]
    public class when_validating_tfchunkdb_without_previous_files : SpecificationWithDirectory
    {
        [Test]
        public void with_a_writer_checksum_of_nonzero_and_no_files_a_corrupted_database_exception_is_thrown()
        {
            var db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                       new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                       10000,
                                                       0,
                                                       new InMemoryCheckpoint(500),
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(-1),
                                                       new InMemoryCheckpoint(-1)));
            var exc = Assert.Throws<CorruptDatabaseException>(() => db.Open());
            Assert.IsInstanceOf<ChunkNotFoundException>(exc.InnerException);
            db.Dispose();
        }

        [Test]
        public void with_a_writer_checksum_of_zero_and_no_files_is_valid()
        {
            var db = new TFChunkDb(new TFChunkDbConfig(PathName,
                                                       new VersionedPatternFileNamingStrategy(PathName, "chunk-"),
                                                       10000,
                                                       0,
                                                       new InMemoryCheckpoint(0),
                                                       new InMemoryCheckpoint(),
                                                       new InMemoryCheckpoint(-1),
                                                       new InMemoryCheckpoint(-1)));
            Assert.DoesNotThrow(() => db.Open());
            db.Dispose();
        }
    }
}