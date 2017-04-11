using System.IO;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_destroying_a_tfchunk: SpecificationWithFile
    {
        private TFChunk _chunk;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _chunk = TFChunk.CreateNew(Filename, 1000, 0, 0, false);
            _chunk.MarkForDeletion();
        }

        [Test]
        public void the_file_is_deleted()
        {
            Assert.IsFalse(File.Exists(Filename));
        }
    }
}