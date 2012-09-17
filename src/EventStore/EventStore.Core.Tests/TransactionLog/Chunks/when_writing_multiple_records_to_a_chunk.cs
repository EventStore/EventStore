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
    public class when_writing_multiple_records_to_a_chunk
    {
        private readonly string _filename = Path.Combine(Path.GetTempPath(), "foo");
        private TFChunk _chunk;
        private readonly Guid _corrId = Guid.NewGuid();
        private readonly Guid _eventId = Guid.NewGuid();
        private long _position1;
        private long _position2;
        private bool _written1;
        private bool _written2;

        private PrepareLogRecord _prepare1;
        private PrepareLogRecord _prepare2;

        [SetUp]
        public void Setup()
        {
            _prepare1 = new PrepareLogRecord(0, _corrId, _eventId, 0, "test", 1, new DateTime(2000, 1, 1, 12, 0, 0),
                                             PrepareFlags.None, "Foo", new byte[12], new byte[15]);
            _prepare2 = new PrepareLogRecord(0, _corrId, _eventId, 0, "test2", 2, new DateTime(2000, 1, 1, 12, 0, 0),
                                             PrepareFlags.None, "Foo2", new byte[12], new byte[15]);

            _chunk = TFChunk.CreateNew(_filename, 4096, 0, 0);
            var r1 = _chunk.TryAppend(_prepare1);
            _written1 = r1.Success;
            _position1 = r1.OldPosition;
            var r2 = _chunk.TryAppend(_prepare2);
            _written2 = r2.Success;
            _position2 = r2.OldPosition;
            _chunk.Flush();
        }

        [TearDown]
        public void TearDown()
        {
            _chunk.Dispose();
        }

        [Test]
        public void the_chunk_is_not_cached()
        {
            Assert.IsFalse(_chunk.IsCached);
        }

        [Test]
        public void the_first_record_was_written()
        {
            Assert.IsTrue(_written1);
        }

        [Test]
        public void the_second_record_was_written()
        {
            Assert.IsTrue(_written1);
        }

        [Test]
        public void the_first_record_can_be_read()
        {
            var res = _chunk.TryReadRecordAt((int)_position1);
            Assert.IsTrue(res.Success);
            Assert.IsTrue(res.LogRecord is PrepareLogRecord);
            Assert.AreEqual(_prepare1, res.LogRecord);
        }

        [Test]
        public void the_second_record_can_be_read()
        {
            var res = _chunk.TryReadRecordAt((int)_position2);
            Assert.IsTrue(res.Success);
            Assert.IsTrue(res.LogRecord is PrepareLogRecord);
            Assert.AreEqual(_prepare2, res.LogRecord);
        }
    }
}