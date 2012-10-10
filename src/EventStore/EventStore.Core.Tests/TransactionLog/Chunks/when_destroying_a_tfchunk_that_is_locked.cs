using System.IO;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_destroying_a_tfchunk_that_is_locked
    {
        private readonly string _filename = Path.Combine(Path.GetTempPath(), "foo");
        private TFChunk _chunk;
        private TFChunkBulkReader _reader;

        [SetUp]
        public void setup()
        {
            _chunk = TFChunk.CreateNew(_filename, 1000, 0, 0);
            _reader = _chunk.AcquireReader();
            _chunk.MarkForDeletion();
        }

        [Test]
        public void the_file_is_not_deleted()
        {
            Assert.IsTrue(File.Exists(_filename));
        }

        [TearDown]
        public void td()
        {
            _reader.Release();
            _chunk.MarkForDeletion();
            _chunk.WaitForDestroy(2000);
        }
    }
}