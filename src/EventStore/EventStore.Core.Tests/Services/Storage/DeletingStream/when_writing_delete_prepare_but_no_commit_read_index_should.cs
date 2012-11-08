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
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream
{
    [TestFixture]
    public class when_writing_delete_prepare_but_no_commit_read_index_should : ReadIndexTestScenario
    {
        private EventRecord _event1;
        private EventRecord _event2;
        private EventRecord _event3;

        protected override void WriteTestScenario()
        {
            _event1 = WriteStreamCreated("ES");
            _event2 = WriteSingleEvent("ES", 1, "bla1");

            var prepare = LogRecord.DeleteTombstone(WriterCheckpoint.ReadNonFlushed(),
                                                    Guid.NewGuid(),
                                                    "ES",
                                                    2);
            long pos;
            Assert.IsTrue(Writer.Write(prepare, out pos));

            _event3 = WriteSingleEvent("ES", 2, "bla1");
        }

        [Test]
        public void indicate_that_stream_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ES"), Is.False);
        }

        [Test]
        public void indicate_that_nonexisting_stream_with_same_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ZZ"), Is.False);
        }

        [Test]
        public void indicate_that_nonexisting_stream_with_different_hash_is_not_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
        }

        [Test]
        public void read_single_events_should_return_commited_records()
        {
            var result = ReadIndex.ReadEvent("ES", 0);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_event1, result.Record);

            result = ReadIndex.ReadEvent("ES", 1);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_event2, result.Record);

            result = ReadIndex.ReadEvent("ES", 2);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_event3, result.Record);
        }

        [Test]
        public void read_stream_events_forward_should_return_commited_records()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES", 0, 100);
            Assert.AreEqual(RangeReadResult.Success, result.Result);
            Assert.AreEqual(_event1, result.Records[0]);
            Assert.AreEqual(_event2, result.Records[1]);
            Assert.AreEqual(_event3, result.Records[2]);
        }

        [Test]
        public void read_stream_events_backward_should_return_commited_records()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
            Assert.AreEqual(RangeReadResult.Success, result.Result);
            Assert.AreEqual(_event1, result.Records[2]);
            Assert.AreEqual(_event2, result.Records[1]);
            Assert.AreEqual(_event3, result.Records[0]);
        }

        [Test]
        public void read_all_forward_should_return_all_stream_records_except_uncommited()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(3, events.Length);
            Assert.AreEqual(_event1, events[0]);
            Assert.AreEqual(_event2, events[1]);
            Assert.AreEqual(_event3, events[2]);
        }

        [Test]
        public void read_all_backward_should_return_all_stream_records_except_uncommited()
        {
            var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(3, events.Length);
            Assert.AreEqual(_event1, events[2]);
            Assert.AreEqual(_event2, events[1]);
            Assert.AreEqual(_event3, events[0]);
        }
    }
}