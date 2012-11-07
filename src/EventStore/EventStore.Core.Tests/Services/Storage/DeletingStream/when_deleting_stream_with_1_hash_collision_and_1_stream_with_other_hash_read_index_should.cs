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
    public class when_deleting_stream_with_1_hash_collision_and_1_stream_with_other_hash_read_index_should : ReadIndexTestScenario
    {
        protected override void WriteTestScenario()
        {
            WriteStreamCreated("S1");
            WriteSingleEvent("S1", 1, "bla1");
            WriteStreamCreated("S2");
            WriteSingleEvent("S1", 2, "bla1");
            WriteSingleEvent("S2", 1, "bla1");
            WriteSingleEvent("S2", 2, "bla1");
            WriteStreamCreated("SSS");
            WriteSingleEvent("S1", 3, "bla1");
            WriteSingleEvent("SSS", 1, "bla1");

            WriteDelete("S1");
        }

        [Test]
        public void indicate_that_stream_is_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("S1"));
        }

        [Test]
        public void indicate_that_other_stream_with_same_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("S2"), Is.False);
        }

        [Test]
        public void indicate_that_other_stream_with_different_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("SSS"), Is.False);
        }

        [Test]
        public void indicate_that_not_existing_stream_with_same_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("XX"), Is.False);
        }

        [Test]
        public void indicate_that_not_existing_stream_with_different_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
        }
    }
}