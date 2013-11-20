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
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.BuildingIndex
{
    [TestFixture]
    public class when_building_an_index_off_tfile_with_two_events_in_stream : ReadIndexTestScenario
    {
        private Guid _id1;
        private Guid _id2;

        private PrepareLogRecord _prepare1;
        private PrepareLogRecord _prepare2;

        protected override void WriteTestScenario()
        {
            _id1 = Guid.NewGuid();
            _id2 = Guid.NewGuid();

            long pos1, pos2, pos3, pos4;
            _prepare1 = new PrepareLogRecord(0, _id1, _id1, 0, 0, "test1", ExpectedVersion.NoStream, DateTime.UtcNow,
                                             PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]);
            Writer.Write(_prepare1, out pos1);
            _prepare2 = new PrepareLogRecord(pos1, _id2, _id2, pos1, 0, "test1", 0, DateTime.UtcNow,
                                             PrepareFlags.SingleWrite, "type", new byte[0], new byte[0]);
            Writer.Write(_prepare2, out pos2);
            Writer.Write(new CommitLogRecord(pos2, _id1, 0, DateTime.UtcNow, 0), out pos3);
            Writer.Write(new CommitLogRecord(pos3, _id2, pos1, DateTime.UtcNow, 1), out pos4);
        }

        [Test]
        public void the_first_event_can_be_read()
        {
            var result = ReadIndex.ReadEvent("test1", 0);
            Assert.AreEqual(ReadEventResult.Success, result.Result);
            Assert.AreEqual(new EventRecord(0, _prepare1), result.Record);
        }

        [Test]
        public void the_second_event_can_be_read()
        {
            var result = ReadIndex.ReadEvent("test1", 1);
            Assert.AreEqual(ReadEventResult.Success, result.Result);
            Assert.AreEqual(new EventRecord(1, _prepare2), result.Record);
        }

        [Test]
        public void the_nonexisting_event_can_not_be_read()
        {
            var result = ReadIndex.ReadEvent("test1", 2);
            Assert.AreEqual(ReadEventResult.NotFound, result.Result);
        }

        [Test]
        public void the_last_event_can_be_read_and_is_correct()
        {
            var result = ReadIndex.ReadEvent("test1", -1);
            Assert.AreEqual(ReadEventResult.Success, result.Result);
            Assert.AreEqual(new EventRecord(1, _prepare2), result.Record);
        }

        [Test]
        public void the_first_event_can_be_read_through_range_query()
        {
            var result = ReadIndex.ReadStreamEventsBackward("test1", 0, 1);
            Assert.AreEqual(ReadStreamResult.Success, result.Result);
            Assert.AreEqual(1, result.Records.Length);
            Assert.AreEqual(new EventRecord(0, _prepare1), result.Records[0]);
        }

        [Test]
        public void the_second_event_can_be_read_through_range_query()
        {
            var result = ReadIndex.ReadStreamEventsBackward("test1", 1, 1);
            Assert.AreEqual(ReadStreamResult.Success, result.Result);
            Assert.AreEqual(1, result.Records.Length);
            Assert.AreEqual(new EventRecord(1, _prepare2), result.Records[0]);
        }

        [Test]
        public void the_stream_can_be_read_as_a_whole_with_specific_from_version()
        {
            var result = ReadIndex.ReadStreamEventsBackward("test1", 1, 2);
            Assert.AreEqual(ReadStreamResult.Success, result.Result);
            Assert.AreEqual(2, result.Records.Length);
            Assert.AreEqual(new EventRecord(1, _prepare2), result.Records[0]);
            Assert.AreEqual(new EventRecord(0, _prepare1), result.Records[1]);
        }

        [Test]
        public void the_stream_can_be_read_as_a_whole_with_from_end()
        {
            var result = ReadIndex.ReadStreamEventsBackward("test1", -1, 2);
            Assert.AreEqual(ReadStreamResult.Success, result.Result);
            Assert.AreEqual(2, result.Records.Length);
            Assert.AreEqual(new EventRecord(1, _prepare2), result.Records[0]);
            Assert.AreEqual(new EventRecord(0, _prepare1), result.Records[1]);
        }

        [Test]
        public void the_stream_cant_be_read_for_second_stream()
        {
            var result = ReadIndex.ReadStreamEventsBackward("test2", 0, 1);
            Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
            Assert.AreEqual(0, result.Records.Length);
        }

        [Test]
        public void read_all_events_forward_returns_all_events_in_correct_order()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 10).Records;

            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(_id1, records[0].Event.EventId);
            Assert.AreEqual(_id2, records[1].Event.EventId);
        }

        [Test]
        public void read_all_events_backward_returns_all_events_in_correct_order()
        {
            var records = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 10).Records;

            Assert.AreEqual(2, records.Count);
            Assert.AreEqual(_id1, records[1].Event.EventId);
            Assert.AreEqual(_id2, records[0].Event.EventId);
        }
    }
}