using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Core.Tests.TransactionLog
{
    [TestFixture]
    public class when_reading_physical_bytes_bulk_from_a_chunk : SpecificationWithDirectory
    {

        [Test]
        public void the_file_will_not_be_deleted_until_reader_released()
        {
            TFChunk chunk = null;

            try
            {
                chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, 0, false, false, false, false);
                using (var reader = chunk.AcquireReader())
                {
                    chunk.MarkForDeletion();
                    var buffer = new byte[1024];
                    var result = reader.ReadNextRawBytes(1024, buffer);
                    Assert.IsFalse(result.IsEOF);
                    Assert.AreEqual(1024, result.BytesRead);
                }
            }
            finally
            {
                if (chunk != null)
                    chunk.WaitForDestroy(5000);
            }
        }

        [Test]
        public void a_read_on_new_file_can_be_performed()
        {
            TFChunk chunk = null;

            try
            {
                chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, 0, false, false, false, false);
                using (var reader = chunk.AcquireReader())
                {
                    var buffer = new byte[1024];
                    var result = reader.ReadNextRawBytes(1024, buffer);
                    Assert.IsFalse(result.IsEOF);
                    Assert.AreEqual(1024, result.BytesRead);
                }
            }
            finally
            {
                if (chunk != null)
                {
                    chunk.MarkForDeletion();
                    chunk.WaitForDestroy(5000);
                }
            }
        }

        [Test]
        public void if_asked_for_more_than_buffer_size_will_only_read_buffer_size()
        {
            TFChunk chunk = null;

            try
            {
                chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 3000, 0, 0, false, false, false, false);
                using (var reader = chunk.AcquireReader())
                {
                    var buffer = new byte[1024];
                    var result = reader.ReadNextRawBytes(3000, buffer);
                    Assert.IsFalse(result.IsEOF);
                    Assert.AreEqual(1024, result.BytesRead);
                }
            }
            finally
            {
                if (chunk != null)
                {
                    chunk.MarkForDeletion();
                    chunk.WaitForDestroy(5000);
                }
            }
        }

        [Test]
        public void a_read_past_eof_returns_eof()
        {
            TFChunk chunk = null;

            try
            {
                chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 300, 0, 0, false, false, false, false);
                using (var reader = chunk.AcquireReader())
                {
                    var buffer = new byte[1024];
                    var result = reader.ReadNextRawBytes(1024, buffer);
                    Assert.IsTrue(result.IsEOF);
                    Assert.AreEqual(ChunkHeader.Size + ChunkFooter.Size + 300, result.BytesRead);
                }
            }
            finally
            {
                if (chunk != null)
                {
                    chunk.MarkForDeletion();
                    chunk.WaitForDestroy(5000);
                }
            }
        }
    }
}