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
    public class with_invalid_max_count_and_normal_max_age: ReadIndexTestScenario
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

            const string metadata = @"{""$maxAge"":21,""$maxCount"":2.1}";

            _r1 = WriteStreamCreated("ES", metadata, now.AddSeconds(-100));
            _r2 = WriteSingleEvent("ES", 1, "bla1", now.AddSeconds(-50));
            _r3 = WriteSingleEvent("ES", 2, "bla1", now.AddSeconds(-20));
            _r4 = WriteSingleEvent("ES", 3, "bla1", now.AddSeconds(-11));
            _r5 = WriteSingleEvent("ES", 4, "bla1", now.AddSeconds(-5));
            _r6 = WriteSingleEvent("ES", 5, "bla1", now.AddSeconds(-1));
        }

        [Test]
        public void on_single_event_read_invalid_value_is_ignored()
        {
            var result = ReadIndex.ReadEvent("ES", 0);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES", 1);
            Assert.AreEqual(SingleReadResult.NotFound, result.Result);
            Assert.IsNull(result.Record);

            result = ReadIndex.ReadEvent("ES", 2);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r3, result.Record);

            result = ReadIndex.ReadEvent("ES", 3);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r4, result.Record);

            result = ReadIndex.ReadEvent("ES", 4);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r5, result.Record);

            result = ReadIndex.ReadEvent("ES", 5);
            Assert.AreEqual(SingleReadResult.Success, result.Result);
            Assert.AreEqual(_r6, result.Record);
        }

        [Test]
        public void on_forward_range_read_invalid_value_is_ignored()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES", 0, 100);
            Assert.AreEqual(RangeReadResult.Success, result.Result);
            Assert.AreEqual(4, result.Records.Length);
            Assert.AreEqual(_r3, result.Records[0]);
            Assert.AreEqual(_r4, result.Records[1]);
            Assert.AreEqual(_r5, result.Records[2]);
            Assert.AreEqual(_r6, result.Records[3]);
        }

        [Test]
        public void on_backward_range_read_invalid_value_is_ignored()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES", -1, 100);
            Assert.AreEqual(RangeReadResult.Success, result.Result);
            Assert.AreEqual(4, result.Records.Length);
            Assert.AreEqual(_r6, result.Records[0]);
            Assert.AreEqual(_r5, result.Records[1]);
            Assert.AreEqual(_r4, result.Records[2]);
            Assert.AreEqual(_r3, result.Records[3]);
        }

        [Test]
        public void on_read_all_forward_both_values_are_ignored()
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
        public void on_read_all_backward_both_values_are_ignored()
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
