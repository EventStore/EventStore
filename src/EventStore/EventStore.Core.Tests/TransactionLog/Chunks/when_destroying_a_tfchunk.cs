using System.IO;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_destroying_a_tfchunk
    {
        private readonly string _filename = Path.Combine(Path.GetTempPath(), "foo");
        private TFChunk _chunk;

        [SetUp]
        public void Setup()
        {
            _chunk = TFChunk.CreateNew(_filename, 1000, 0, 0);
            _chunk.MarkForDeletion();
        }

        [Test]
        public void the_file_is_deleted()
        {
            Assert.IsFalse(File.Exists(_filename));
        }
    }
}