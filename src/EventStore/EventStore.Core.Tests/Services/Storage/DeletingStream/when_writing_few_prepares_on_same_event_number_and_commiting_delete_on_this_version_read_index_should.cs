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
    public class when_writing_few_prepares_with_same_event_number_and_commiting_delete_on_this_version_read_index_should : ReadIndexTestScenario
    {
        private EventRecord _event1;

        protected override void WriteTestScenario()
        {
            _event1 = WriteStreamCreated("ES");

            long pos;

            var prepare1 = LogRecord.SingleWrite(WriterCheckpoint.ReadNonFlushed(),    // prepare1
                                                              Guid.NewGuid(),
                                                              Guid.NewGuid(),
                                                              "ES",
                                                              0,
                                                              "some-type",
                                                              EventRecord.Empty,
                                                              null,
                                                              DateTime.UtcNow);
            Assert.IsTrue(Writer.Write(prepare1, out pos));

            var prepare2 = LogRecord.SingleWrite(WriterCheckpoint.ReadNonFlushed(),    // prepare2
                                                              Guid.NewGuid(),
                                                              Guid.NewGuid(),
                                                              "ES",
                                                              0,
                                                              "some-type",
                                                              EventRecord.Empty,
                                                              null,
                                                              DateTime.UtcNow);
            Assert.IsTrue(Writer.Write(prepare2, out pos));

            
            var deleteprepare = LogRecord.DeleteTombstone(WriterCheckpoint.ReadNonFlushed(),  // delete prepare
                                                                       Guid.NewGuid(),
                                                                       "ES",
                                                                       2);

            var prepare3 = LogRecord.SingleWrite(WriterCheckpoint.ReadNonFlushed(),     // prepare3
                                                              Guid.NewGuid(),
                                                              Guid.NewGuid(),
                                                              "ES",
                                                              0,
                                                              "some-type",
                                                              EventRecord.Empty,
                                                              null,
                                                              DateTime.UtcNow);
            Assert.IsTrue(Writer.Write(prepare3, out pos));

            var commit = LogRecord.Commit(WriterCheckpoint.ReadNonFlushed(),     // committing delete
                                          deleteprepare.CorrelationId,
                                          deleteprepare.LogPosition,
                                          EventNumber.DeletedStream);
            Assert.IsTrue(Writer.Write(commit, out pos));
        }

        [Test]
        public void indicate_that_stream_is_deleted()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ES"));
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
        public void read_single_events_with_number_0_should_return_stream_deleted()
        {
            EventRecord rec;
            Assert.AreEqual(SingleReadResult.StreamDeleted, ReadIndex.ReadEvent("ES", 0, out rec));
        }

        [Test]
        public void read_single_events_with_number_1_should_return_stream_deleted()
        {
            EventRecord rec;
            Assert.AreEqual(SingleReadResult.StreamDeleted, ReadIndex.ReadEvent("ES", 1, out rec));
            Assert.IsNull(rec);
        }

        [Test]
        public void read_stream_events_forward_should_return_stream_deleted()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.StreamDeleted, ReadIndex.ReadStreamEventsForward("ES", 0, 100, out records));
            Assert.IsNull(records);
        }

        [Test]
        public void read_stream_events_backward_should_return_stream_deleted()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.StreamDeleted, ReadIndex.ReadStreamEventsBackward("ES", -1, 100, out records));
            Assert.IsNull(records);
        }

        [Test]
        public void read_all_forward_should_return_all_stream_records_except_uncommited()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(_event1, events[0]);
        }

        [Test]
        public void read_all_backward_should_return_all_stream_records_except_uncommited()
        {
            var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(_event1, events[0]);
        }
    }
}