// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_reading_physical_bytes_bulk_from_a_chunk : SpecificationWithDirectory
    {
        
        [Test]
        public void the_file_will_not_be_deleted_until_reader_released()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, false);
            using (var reader = chunk.AcquireReader())
            {
                chunk.MarkForDeletion();
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsFalse(result.IsEOF);
                Assert.AreEqual(1024, result.BytesRead);
            }
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_on_new_file_can_be_performed()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 2000, 0, false);
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsFalse(result.IsEOF);
                Assert.AreEqual(1024, result.BytesRead);
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_on_scavenged_chunk_includes_map()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("afile"), 200, 0, false);
            chunk.CompleteScavenge(new [] {new PosMap(0, 0), new PosMap(1,1) });
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsTrue(result.IsEOF);
                Assert.AreEqual(272, result.BytesRead); //header 128 + footer 128 + map 16
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_past_end_of_completed_chunk_does_include_header_or_footer()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("File1"), 300, 0, false);
            chunk.Complete();
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsTrue(result.IsEOF);
                Assert.AreEqual(ChunkHeader.Size + ChunkFooter.Size, result.BytesRead); //just header + footer = 256
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void if_asked_for_more_than_buffer_size_will_only_read_buffer_size()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 3000, 0, false);
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(3000, buffer);
                Assert.IsFalse(result.IsEOF);
                Assert.AreEqual(1024, result.BytesRead);
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }

        [Test]
        public void a_read_past_eof_returns_eof_and_no_footer()
        {
            var chunk = TFChunk.CreateNew(GetFilePathFor("file1"), 300, 0, false);
            using (var reader = chunk.AcquireReader())
            {
                var buffer = new byte[1024];
                var result = reader.ReadNextPhysicalBytes(1024, buffer);
                Assert.IsTrue(result.IsEOF);
                Assert.AreEqual(556, result.BytesRead); //does not includes header and footer space
            }
            chunk.MarkForDeletion();
            chunk.WaitForDestroy(5000);
        }
    }
}