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

using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream
{
    [TestFixture]
    public class when_deleting_streams_with_different_hashes_spanning_through_multiple_chunks_in_db_with_1_stream_with_same_hash_read_index_should : ReadIndexTestScenario
    {

        protected override void WriteTestScenario()
        {
            WriteSingleEvent("ES1", 0, new string('.', 3000));
            WriteSingleEvent("ES1", 1, new string('.', 3000));
            WriteSingleEvent("ES2", 0, new string('.', 3000));

            WriteSingleEvent("ES", 0, new string('.', 3000), retryOnFail: true); // chunk 2
            WriteSingleEvent("ES", 1, new string('.', 3000));
            WriteSingleEvent("ES1", 2, new string('.', 3000));

            WriteSingleEvent("ES2", 1, new string('.', 3000), retryOnFail: true); // chunk 3
            WriteSingleEvent("ES1", 3, new string('.', 3000));
            WriteSingleEvent("ES1", 4, new string('.', 3000));

            WriteSingleEvent("ES2", 2, new string('.', 3000), retryOnFail: true); // chunk 4
            WriteSingleEvent("ES", 2, new string('.', 3000));
            WriteSingleEvent("ES", 3, new string('.', 3000));

            WriteDelete("ES1");
            WriteDelete("ES");
        }

        [Test]
        public void indicate_that_streams_are_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ES1"));
            Assert.That(ReadIndex.IsStreamDeleted("ES"));
        }

        [Test]
        public void indicate_that_nonexisting_streams_with_same_hashes_are_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
            Assert.That(ReadIndex.IsStreamDeleted("XX"), Is.False);
        }

        [Test]
        public void indicate_that_nonexisting_stream_with_different_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("XXXX"), Is.False);
        }

        [Test]
        public void indicate_that_existing_stream_with_different_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ES2"), Is.False);
        }
    }
}