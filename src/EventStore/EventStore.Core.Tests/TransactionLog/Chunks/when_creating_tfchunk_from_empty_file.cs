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
using System;
using System.IO;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_creating_tfchunk_from_empty_file: SpecificationWithFile
    {
        private TFChunk _chunk;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            _chunk = TFChunk.CreateNew(Filename, 1024, 0, 0);
        }

        [TearDown]
        public override void TearDown()
        {
            _chunk.Dispose();
            base.TearDown();
        }

        [Test]
        public void the_chunk_is_not_cached()
        {
            Assert.IsFalse(_chunk.IsCached);
        }

        [Test]
        public void the_file_is_created()
        {
            Assert.IsTrue(File.Exists(Filename));
        }

        [Test]
        public void the_chunk_is_not_readonly()
        {
            Assert.IsFalse(_chunk.IsReadOnly);
        }

        [Test]
        public void append_does_not_throw_exception()
        {
            Assert.DoesNotThrow(() => _chunk.TryAppend(new CommitLogRecord(0, Guid.NewGuid(), 0, DateTime.UtcNow, 0)));
        }

        [Test]
        public void there_is_no_record_at_pos_zero()
        {
            var res = _chunk.TryReadAt(0);
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void there_is_no_first_record()
        {
            var res = _chunk.TryReadFirst();
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void there_is_no_closest_forward_record_to_pos_zero()
        {
            var res = _chunk.TryReadClosestForward(0);
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void there_is_no_closest_backward_record_from_end()
        {
            var res = _chunk.TryReadClosestForward(0);
            Assert.IsFalse(res.Success);
        }

        [Test]
        public void there_is_no_last_record()
        {
            var res = _chunk.TryReadLast();
            Assert.IsFalse(res.Success);
        }
    }
}
