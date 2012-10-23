using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_reading_bulk_a_chunk : SpecificationWithDirectory
    {
        [Test]
        public void the_file_will_not_be_deleted_until_reader_released()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, 0);
            using (var reader = chunk.AcquireReader())
            {
                chunk.MarkForDeletion();
                var buffer = new byte[1024];
                var result = reader.ReadNextBytes(1024, buffer);
                Assert.IsFalse(result.IsEOF);
                Assert.AreEqual(1024, result.ReadData);
            }
            chunk.WaitForDestroy(5000);
        }
        [Test]
        public void a_read_on_new_file_can_be_performed()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, 0);
            using(var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextBytes(1024, buffer);
                Assert.IsFalse(result.IsEOF);
                Assert.AreEqual(1024, result.ReadData);
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_past_end_of_completed_chunk_includes_footer()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 300, 0, 0);
            chunk.Complete();
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextBytes(1024, buffer);
                Assert.IsTrue(result.IsEOF);
                Assert.AreEqual(556, result.ReadData); //includes header of 128 header and footer
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_past_eof_returns_eof_and_partial_read()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 300, 0, 0);
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextBytes(1024, buffer);
                Assert.IsTrue(result.IsEOF);
                Assert.AreEqual(556, result.ReadData); //includes header and footer space
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }
    }
}