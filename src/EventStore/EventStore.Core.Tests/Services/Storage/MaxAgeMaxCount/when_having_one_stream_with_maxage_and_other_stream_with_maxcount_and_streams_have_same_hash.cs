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
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount
{
    [TestFixture]
    public class when_having_one_stream_with_maxage_and_other_stream_with_maxcount_and_streams_have_same_hash : ReadIndexTestScenario
    {
        private EventRecord _r11;
        private EventRecord _r12;
        private EventRecord _r13;
        private EventRecord _r14;
        private EventRecord _r15;
        private EventRecord _r16;

        private EventRecord _r21;
        private EventRecord _r22;
        private EventRecord _r23;
        private EventRecord _r24;
        private EventRecord _r25;
        private EventRecord _r26;

        protected override void WriteTestScenario()
        {
            var now = DateTime.UtcNow;

            var metadata1 = string.Format(@"{{""$maxAge"":{0}}}", (int)TimeSpan.FromMinutes(25).TotalSeconds);
            const string metadata2 = @"{""$maxCount"":2}";

            _r11 = WriteStreamCreated("ES1", metadata1, now.AddMinutes(-100));
            _r21 = WriteStreamCreated("ES2", metadata2, now.AddMinutes(-99));

            _r12 = WriteSingleEvent("ES1", 1, "bla1", now.AddMinutes(-50));
            _r13 = WriteSingleEvent("ES1", 2, "bla1", now.AddMinutes(-20));

            _r22 = WriteSingleEvent("ES2", 1, "bla1", now.AddMinutes(-20));
            _r23 = WriteSingleEvent("ES2", 2, "bla1", now.AddMinutes(-19));

            _r14 = WriteSingleEvent("ES1", 3, "bla1", now.AddMinutes(-11));
            _r24 = WriteSingleEvent("ES2", 3, "bla1", now.AddMinutes(-10));

            _r15 = WriteSingleEvent("ES1", 4, "bla1", now.AddMinutes(-5));
            _r16 = WriteSingleEvent("ES1", 5, "bla1", now.AddMinutes(-2));

            _r25 = WriteSingleEvent("ES2", 4, "bla1", now.AddMinutes(-1));
            _r26 = WriteSingleEvent("ES2", 5, "bla1", now.AddMinutes(-1));
        }

        [Test]
        public void single_event_read_doesnt_return_stream_created_event_for_both_streams()
        {
            var result = ReadIndex.ReadEvent("ES1", 0);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES2", 0);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);
        }

        [Test]
        public void single_event_read_doesnt_return_expired_events_and_returns_all_actual_ones_for_stream_1()
        {
            var result = ReadIndex.ReadEvent("ES1", 0);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES1", 1);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES1", 2);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r13, result.Record);

            result = ReadIndex.ReadEvent("ES1", 3);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r14, result.Record);

            result = ReadIndex.ReadEvent("ES1", 4);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r15, result.Record);

            result = ReadIndex.ReadEvent("ES1", 5);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r16, result.Record);
        }

        [Test]
        public void single_event_read_doesnt_return_expired_events_and_returns_all_actual_ones_for_stream_2()
        {
            var result = ReadIndex.ReadEvent("ES2", 0);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES2", 1);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES2", 2);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES2", 3);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES2", 4);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r25, result.Record);

            result = ReadIndex.ReadEvent("ES2", 5);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r26, result.Record);
        }

        [Test]
        public void forward_range_read_doesnt_return_expired_records_for_stream_1()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES1", 0, 100);
            Assert.AreEqual(RangeReadResult.Success, result.Result);
            Assert.AreEqual(4, result.Records.Length);
            Assert.AreEqual(_r13, result.Records[0]);
            Assert.AreEqual(_r14, result.Records[1]);
            Assert.AreEqual(_r15, result.Records[2]);
            Assert.AreEqual(_r16, result.Records[3]);
        }

        [Test]
        public void forward_range_read_doesnt_return_expired_records_for_stream_2()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES2", 0, 100);
            Assert.AreEqual(RangeReadResult.Success, result.Result);
            Assert.AreEqual(2, result.Records.Length);
            Assert.AreEqual(_r25, result.Records[0]);
            Assert.AreEqual(_r26, result.Records[1]);
        }

        [Test]
        public void backward_range_read_doesnt_return_expired_records_for_stream_1()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES1", -1, 100);
            Assert.AreEqual(RangeReadResult.Success, result.Result);
            Assert.AreEqual(4, result.Records.Length);
            Assert.AreEqual(_r16, result.Records[0]);
            Assert.AreEqual(_r15, result.Records[1]);
            Assert.AreEqual(_r14, result.Records[2]);
            Assert.AreEqual(_r13, result.Records[3]);
        }

        [Test]
        public void backward_range_read_doesnt_return_expired_records_for_stream_2()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES2", -1, 100);
            Assert.AreEqual(RangeReadResult.Success, result.Result);
            Assert.AreEqual(2, result.Records.Length);
            Assert.AreEqual(_r26, result.Records[0]);
            Assert.AreEqual(_r25, result.Records[1]);
        }

        [Test]
        public void read_all_forward_returns_all_records_including_expired_ones()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
            Assert.AreEqual(12, records.Count);
            Assert.AreEqual(_r11, records[0].Event);
            Assert.AreEqual(_r21, records[1].Event);

            Assert.AreEqual(_r12, records[2].Event);
            Assert.AreEqual(_r13, records[3].Event);

            Assert.AreEqual(_r22, records[4].Event);
            Assert.AreEqual(_r23, records[5].Event);
            
            Assert.AreEqual(_r14, records[6].Event);
            Assert.AreEqual(_r24, records[7].Event);

            Assert.AreEqual(_r15, records[8].Event);
            Assert.AreEqual(_r16, records[9].Event);

            Assert.AreEqual(_r25, records[10].Event);
            Assert.AreEqual(_r26, records[11].Event);
        }

        [Test]
        public void read_all_backward_returns_all_records_including_expired_ones()
        {
            var records = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records;
            Assert.AreEqual(12, records.Count);
            Assert.AreEqual(_r11, records[11].Event);
            Assert.AreEqual(_r21, records[10].Event);

            Assert.AreEqual(_r12, records[9].Event);
            Assert.AreEqual(_r13, records[8].Event);

            Assert.AreEqual(_r22, records[7].Event);
            Assert.AreEqual(_r23, records[6].Event);

            Assert.AreEqual(_r14, records[5].Event);
            Assert.AreEqual(_r24, records[4].Event);

            Assert.AreEqual(_r15, records[3].Event);
            Assert.AreEqual(_r16, records[2].Event);

            Assert.AreEqual(_r25, records[1].Event);
            Assert.AreEqual(_r26, records[0].Event);
        }
    }
}