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
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount
{
    [TestFixture]
    public class when_having_stream_both_maxage_and_maxcount_specified_with_maxcount_more_strict : ReadIndexTestScenario
    {
        private EventRecord _r1;
        private EventRecord _r2;
        private EventRecord _r3;
        private EventRecord _r4;
        private EventRecord _r5;
        private EventRecord _r6;

        protected override void WriteTestScenario()
        {
            var now = DateTime.UtcNow;

            const string metadata = @"{""$maxAge"":60,""$maxCount"":3}";

            _r1 = WriteStreamCreated("ES", metadata, now.AddSeconds(-100));
            _r2 = WriteSingleEvent("ES", 1, "bla1", now.AddSeconds(-50));
            _r3 = WriteSingleEvent("ES", 2, "bla1", now.AddSeconds(-20));
            _r4 = WriteSingleEvent("ES", 3, "bla1", now.AddSeconds(-11));
            _r5 = WriteSingleEvent("ES", 4, "bla1", now.AddSeconds(-5));
            _r6 = WriteSingleEvent("ES", 5, "bla1", now.AddSeconds(-1));
        }

        [Test]
        public void single_event_read_doesnt_return_stream_created_event()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 0, out record));
            Assert.IsNull(record);
        }

        [Test]
        public void single_event_read_doesnt_return_expired_events_and_returns_all_actual_ones()
        {
            EventRecord record;
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 0, out record));
            Assert.IsNull(record);
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 1, out record));
            Assert.IsNull(record);
            Assert.AreEqual(SingleReadResult.NotFound, ReadIndex.ReadEvent("ES", 2, out record));
            Assert.IsNull(record);

            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 3, out record));
            Assert.AreEqual(_r4, record);
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 4, out record));
            Assert.AreEqual(_r5, record);
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("ES", 5, out record));
            Assert.AreEqual(_r6, record);
        }

        [Test]
        public void forward_range_read_doesnt_return_expired_records()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("ES", 0, 100, out records));
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_r4, records[0]);
            Assert.AreEqual(_r5, records[1]);
            Assert.AreEqual(_r6, records[2]);
        }

        [Test]
        public void backward_range_read_doesnt_return_expired_records()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("ES", -1, 100, out records));
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_r6, records[0]);
            Assert.AreEqual(_r5, records[1]);
            Assert.AreEqual(_r4, records[2]);
        }

        [Test]
        public void read_all_forward_returns_all_records_including_expired_ones()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
            Assert.AreEqual(6, records.Count);
            Assert.AreEqual(_r1, records[0].Event);
            Assert.AreEqual(_r2, records[1].Event);
            Assert.AreEqual(_r3, records[2].Event);
            Assert.AreEqual(_r4, records[3].Event);
            Assert.AreEqual(_r5, records[4].Event);
            Assert.AreEqual(_r6, records[5].Event);
        }

        [Test]
        public void read_all_backward_returns_all_records_including_expired_ones()
        {
            var records = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records;
            Assert.AreEqual(6, records.Count);
            Assert.AreEqual(_r6, records[0].Event);
            Assert.AreEqual(_r5, records[1].Event);
            Assert.AreEqual(_r4, records[2].Event);
            Assert.AreEqual(_r3, records[3].Event);
            Assert.AreEqual(_r2, records[4].Event);
            Assert.AreEqual(_r1, records[5].Event);
        }
    }
}