using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_reading_physical_bytes_bulk_from_a_chunk : SpecificationWithDirectory
    {
        
        [Test]
        public void the_file_will_not_be_deleted_until_reader_released()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, 0);
            using (var reader = chunk.AcquireReader())
            {
                chunk.MarkForDeletion();
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsFalse(result.IsEOF);
                Assert.AreEqual(1024, result.ReadData);
            }
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_on_new_file_can_be_performed()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, 0);
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsFalse(result.IsEOF);
                Assert.AreEqual(1024, result.ReadData);
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_on_scavenged_chunk_includes_map()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("afile"), 200, 0, 0);
            chunk.CompleteScavenge(new [] {new PosMap(0, 0), new PosMap(1,1) });
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsTrue(result.IsEOF);
                Assert.AreEqual(272, result.ReadData); //header 128 + footer 128 + map 16
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_past_end_of_completed_chunk_does_include_header_or_footer()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("File1"), 300, 0, 0);
            chunk.Complete();
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsTrue(result.IsEOF);
                Assert.AreEqual(ChunkHeader.Size + ChunkFooter.Size, result.ReadData); //just header + footer = 256
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        new
            public void if_asked_for_more_than_buffer_size_will_only_read_buffer_size()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 3000, 0, 0);
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(3000, buffer);
                Assert.IsFalse(result.IsEOF);
                Assert.AreEqual(1024, result.ReadData);
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_past_eof_returns_eof_and_no_footer()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 300, 0, 0);
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsTrue(result.IsEOF);
                Assert.AreEqual(556, result.ReadData); //does not includes header and footer space
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }
    }
}