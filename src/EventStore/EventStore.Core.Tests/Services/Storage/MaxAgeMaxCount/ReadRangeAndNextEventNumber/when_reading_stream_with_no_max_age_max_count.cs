﻿// Copyright (c) 2012, Event Store LLP
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
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.MaxAgeMaxCount.ReadRangeAndNextEventNumber
{
    [TestFixture]
    public class when_reading_stream_with_no_max_age_max_count : ReadIndexTestScenario
    {
        private EventRecord _event0;
        private EventRecord _event1;
        private EventRecord _event2;
        private EventRecord _event3;
        private EventRecord _event4;

        protected override void WriteTestScenario()
        {
            _event0 = WriteSingleEvent("ES", 0, "bla");
            _event1 = WriteSingleEvent("ES", 1, "bla");
            _event2 = WriteSingleEvent("ES", 2, "bla");
            _event3 = WriteSingleEvent("ES", 3, "bla");
            _event4 = WriteSingleEvent("ES", 4, "bla");
        }

        [Test]
        public void on_read_forward_from_start_to_middle_next_event_number_is_middle_plus_1_and_its_not_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsForward("ES", 0, 3);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(3, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsFalse(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_event0, records[0]);
            Assert.AreEqual(_event1, records[1]);
            Assert.AreEqual(_event2, records[2]);
        }

        [Test]
        public void on_read_forward_from_the_middle_to_end_next_event_number_is_end_plus_1_and_its_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsForward("ES", 1, 4);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(5, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsTrue(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(4, records.Length);
            Assert.AreEqual(_event1, records[0]);
            Assert.AreEqual(_event2, records[1]);
            Assert.AreEqual(_event3, records[2]);
            Assert.AreEqual(_event4, records[3]);
        }

        [Test]
        public void on_read_forward_from_the_middle_to_out_of_bounds_next_event_number_is_end_plus_1_and_its_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsForward("ES", 1, 5);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(5, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsTrue(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(4, records.Length);
            Assert.AreEqual(_event1, records[0]);
            Assert.AreEqual(_event2, records[1]);
            Assert.AreEqual(_event3, records[2]);
            Assert.AreEqual(_event4, records[3]);
        }

        [Test]
        public void on_read_forward_from_the_out_of_bounds_to_out_of_bounds_next_event_number_is_end_plus_1_and_its_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsForward("ES", 6, 2);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(5, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsTrue(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(0, records.Length);
        }


        [Test]
        public void on_read_backward_from_the_end_to_middle_next_event_number_is_middle_minus_1_and_its_not_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsBackward("ES", 4, 3);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(1, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsFalse(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_event4, records[0]);
            Assert.AreEqual(_event3, records[1]);
            Assert.AreEqual(_event2, records[2]);
        }

        [Test]
        public void on_read_backward_from_middle_to_start_next_event_number_is_minus_1_and_its_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsBackward("ES", 2, 3);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(-1, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsTrue(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_event2, records[0]);
            Assert.AreEqual(_event1, records[1]);
            Assert.AreEqual(_event0, records[2]);
        }

        [Test]
        public void on_read_backward_from_middle_to_before_start_next_event_number_is_minus_1_and_its_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsBackward("ES", 2, 5);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(-1, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsTrue(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_event2, records[0]);
            Assert.AreEqual(_event1, records[1]);
            Assert.AreEqual(_event0, records[2]);
        }

        [Test]
        public void on_read_backward_from_out_of_bounds_to_middle_next_event_number_is_middle_minus_1_and_its_not_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsBackward("ES", 6, 5);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(1, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsFalse(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(3, records.Length);
            Assert.AreEqual(_event4, records[0]);
            Assert.AreEqual(_event3, records[1]);
            Assert.AreEqual(_event2, records[2]);
        }

        [Test]
        public void on_read_backward_from_out_of_bounds_to_out_of_bounds_next_event_number_is_end_and_its_not_end_of_stream()
        {
            var res = ReadIndex.ReadStreamEventsBackward("ES", 10, 3);
            Assert.AreEqual(ReadStreamResult.Success, res.Result);
            Assert.AreEqual(4, res.NextEventNumber);
            Assert.AreEqual(4, res.LastEventNumber);
            Assert.IsFalse(res.IsEndOfStream);

            var records = res.Records;
            Assert.AreEqual(0, records.Length);
        }
    }
}
