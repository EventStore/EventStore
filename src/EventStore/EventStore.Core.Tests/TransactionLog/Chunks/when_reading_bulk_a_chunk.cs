using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_reading_bulk_a_chunk : SpecificationWithDirectory
    {
        
        [Test]
        public void a_read_on_new_file_can_be_performed()
        {
            base.SetUp();
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, 0);
            using(var reader = chunk.AcquireReader())
            {
                var result = reader.ReadNextBytes(1024);
                Assert.IsFalse(result.IsEOF);
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }
    }
}