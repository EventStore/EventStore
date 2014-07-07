using System.IO;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_unlocking_a_tfchunk_that_has_been_marked_for_deletion: SpecificationWithFile
    {
        private TFChunk _chunk;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _chunk = TFChunk.CreateNew(Filename, 1000, 0, 0, false);
            var reader = _chunk.AcquireReader();
            _chunk.MarkForDeletion();
            reader.Release();
        }

        [Test]
        public void the_file_is_deleted()
        {
            Assert.IsFalse(File.Exists(Filename));
        }
    }
}