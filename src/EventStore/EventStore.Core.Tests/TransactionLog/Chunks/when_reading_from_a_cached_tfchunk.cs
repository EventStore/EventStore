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
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_reading_from_a_cached_tfchunk
    {
        private readonly string _filename = Path.Combine(Path.GetTempPath(), "foo");
        private TFChunk _chunk;
        private readonly Guid _corrId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();
        private TFChunk _cachedChunk;
        private PrepareLogRecord _record;
        private RecordWriteResult _result;

        [SetUp]
        public void Setup()
        {
            _record = new PrepareLogRecord(0, _corrId, _eventId, 0, 0, "test", 1, new DateTime(2000, 1, 1, 12, 0, 0),
                                           PrepareFlags.None, "Foo", new byte[12], new byte[15]);
            _chunk = TFChunk.CreateNew(_filename, 4096, 0, 0);
            _result = _chunk.TryAppend(_record);
            _chunk.Flush();
            _chunk.Complete();
            _cachedChunk = TFChunk.FromCompletedFile(_filename, verifyHash: true);
            _cachedChunk.CacheInMemory();
        }

        [Test]
        public void the_write_result_is_correct()
        {
            Assert.IsTrue(_result.Success);
            Assert.AreEqual(0, _result.OldPosition);
            Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), _result.NewPosition);
        }

        [Test]
        public void the_chunk_is_cached()
        {
            Assert.IsTrue(_cachedChunk.IsCached);
        }

        [Test]
        public void the_record_can_be_read_at_exact_position()
        {
            var res = _cachedChunk.TryReadAt(0);
            Assert.IsTrue(res.Success);
            Assert.AreEqual(_record, res.LogRecord);
            Assert.AreEqual(_result.OldPosition, res.LogRecord.Position);
        }

        [Test]
        public void the_record_can_be_read_as_first_record()
        {
            var res = _cachedChunk.TryReadFirst();
            Assert.IsTrue(res.Success);
            Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), res.NextPosition);
            Assert.AreEqual(_record, res.LogRecord);
            Assert.AreEqual(_result.OldPosition, res.LogRecord.Position);
        }

        [Test]
        public void the_record_can_be_read_as_closest_forward_to_zero_pos()
        {
            var res = _cachedChunk.TryReadClosestForward(0);
            Assert.IsTrue(res.Success);
            Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), res.NextPosition);
            Assert.AreEqual(_record, res.LogRecord);
            Assert.AreEqual(_result.OldPosition, res.LogRecord.Position);
        }

        [Test]
        public void the_record_can_be_read_as_closest_backward_from_end()
        {
            var res = _cachedChunk.TryReadClosestBackward(_record.GetSizeWithLengthPrefixAndSuffix());
            Assert.IsTrue(res.Success);
            Assert.AreEqual(0, res.NextPosition);
            Assert.AreEqual(_record, res.LogRecord);
        }

        [Test]
        public void the_record_can_be_read_as_last()
        {
            var res = _cachedChunk.TryReadLast();
            Assert.IsTrue(res.Success);
            Assert.AreEqual(0, res.NextPosition);
            Assert.AreEqual(_record, res.LogRecord);
        }

        [TearDown]
        public void Teardown()
        {
            _chunk.Dispose();
            _cachedChunk.Dispose();
        }
    }
}