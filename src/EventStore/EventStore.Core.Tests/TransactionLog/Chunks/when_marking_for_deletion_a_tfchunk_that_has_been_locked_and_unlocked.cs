using System.IO;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_marking_for_deletion_a_tfchunk_that_has_been_locked_and_unlocked
    {
        private readonly string _filename = Path.Combine(Path.GetTempPath(), "foo");
        private TFChunk chunk;

        [SetUp]
        public void setup()
        {
            chunk = TFChunk.CreateNew(_filename, 1000, 0, 0);
            var reader = chunk.AcquireReader();
            chunk.MarkForDeletion();
            reader.Release();
        }

        [Test]
        public void the_file_is_deleted()
        {
            Assert.IsFalse(File.Exists(_filename));
        }
    }
}