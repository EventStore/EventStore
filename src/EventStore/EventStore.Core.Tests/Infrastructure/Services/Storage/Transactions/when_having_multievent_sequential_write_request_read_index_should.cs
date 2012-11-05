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

using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage.Transactions
{
    [TestFixture]
    public class when_having_multievent_sequential_write_request_read_index_should : ReadIndexTestScenario
    {
        private EventRecord _p1;
        private EventRecord _p2;
        private EventRecord _p3;

        protected override void WriteTestScenario()
        {
            _p1 = WriteTransactionBegin("ES", ExpectedVersion.NoStream, 0, "test1");
            _p2 = WriteTransactionEvent(_p1.CorrelationId, _p1.LogPosition, 1, _p1.EventStreamId, 1, "test2", PrepareFlags.Data);
            _p3 = WriteTransactionEvent(_p1.CorrelationId, _p1.LogPosition, 2, _p1.EventStreamId, 2, "test3", PrepareFlags.TransactionEnd | PrepareFlags.Data);

            WriteCommit(_p1.CorrelationId, _p1.LogPosition, _p1.EventStreamId, _p1.EventNumber);
        }

        [Test]
        public void return_correct_last_event_version_for_stream()
        {
            Assert.AreEqual(2, ReadIndex.GetLastStreamEventNumber("ES"));
        }

        [Test]
        public void return_correct_first_record_for_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 0, out prepare));
            Assert.AreEqual(_p1, prepare);
        }

        [Test]
        public void return_correct_second_record_for_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 1, out prepare));
            Assert.AreEqual(_p2, prepare);
        }

        [Test]
        public void return_correct_third_record_for_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 2, out prepare));
            Assert.AreEqual(_p3, prepare);
        }

        [Test]
        public void not_find_record_with_nonexistent_version()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 3, out prepare));
        }

        [Test]
        public void return_correct_range_on_from_start_range_query_for_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("ES", 0, 3, out records));
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_p1, records[0]);
            Assert.AreEqual(_p2, records[1]);
            Assert.AreEqual(_p3, records[2]);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_stream_with_specific_event_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ES", 2, 3, out records));
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_p3, records[0]);
            Assert.AreEqual(_p2, records[1]);
            Assert.AreEqual(_p1, records[2]);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_stream_with_from_end_version()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ES", -1, 3, out records));
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_p3, records[0]);
            Assert.AreEqual(_p2, records[1]);
            Assert.AreEqual(_p1, records[2]);
        }

        [Test]
        public void read_all_events_forward_returns_all_events_in_correct_order()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 10).Records;

            Assert.AreEqual(3, records.Count);
            Assert.AreEqual(_p1, records[0].Event);
            Assert.AreEqual(_p2, records[1].Event);
            Assert.AreEqual(_p3, records[2].Event);
        }

        [Test]
        public void read_all_events_backward_returns_all_events_in_correct_order()
        {
            var pos = new TFPos(Db.Config.WriterCheckpoint.Read(), Db.Config.WriterCheckpoint.Read());
            var records = ReadIndex.ReadAllEventsBackward(pos, 10).Records;

            Assert.AreEqual(3, records.Count);
            Assert.AreEqual(_p1, records[2].Event);
            Assert.AreEqual(_p2, records[1].Event);
            Assert.AreEqual(_p3, records[0].Event);
        }
    }
}