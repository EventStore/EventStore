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
using NUnit.Framework;

namespace EventStore.Core.Tests.Infrastructure.Services.Storage
{
    [TestFixture]
    public class with_two_collisioned_streams_one_event_each_first_stream_deleted_read_index_should : ReadIndexTestScenario
    {
        private EventRecord _prepare2;

        protected override void WriteTestScenario()
        {
            WriteSingleEvent("AB", 0, "test1");
            WriteDelete("AB");

            _prepare2 = WriteSingleEvent("CD", 0, "test2");
        }

        [Test]
        public void return_correct_last_event_version_for_first_stream()
        {
            Assert.AreEqual(EventNumber.DeletedStream, ReadIndex.GetLastStreamEventNumber("AB"));
        }

        [Test]
        public void not_find_log_record_for_first_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.StreamDeleted, ReadIndex.ReadEvent("AB", 0, out prepare));
        }

        [Test]
        public void return_empty_range_on_from_start_range_query_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.StreamDeleted, ReadIndex.ReadStreamEventsForward("AB", 0, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_end_range_query_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.StreamDeleted, ReadIndex.ReadStreamEventsBackward("AB", 0, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_start_range_query_for_invalid_arguments_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.StreamDeleted, ReadIndex.ReadStreamEventsForward("AB", 1, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_empty_range_on_from_end_range_query_for_invalid_arguments_for_first_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.StreamDeleted, ReadIndex.ReadStreamEventsBackward("AB", 1, 1, out records));
            Assert.AreEqual(0, records.Length);
        }

        [Test]
        public void return_correct_last_event_version_for_second_stream()
        {
            Assert.AreEqual(0, ReadIndex.GetLastStreamEventNumber("CD"));
        }

        [Test]
        public void return_correct_log_record_for_second_stream()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.Success, ReadIndex.ReadEvent("CD", 0, out prepare));
            Assert.AreEqual(_prepare2, prepare);
        }

        [Test]
        public void return_correct_range_on_from_start_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsForward("CD", 0, 1, out records));
            Assert.AreEqual(_prepare2, records[0]);
        }

        [Test]
        public void return_correct_range_on_from_end_range_query_for_second_stream()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.Success, ReadIndex.ReadStreamEventsBackward("CD", 0, 1, out records));
            Assert.AreEqual(_prepare2, records[0]);
        }

        [Test]
        public void return_correct_last_event_version_for_nonexistent_stream_with_same_hash()
        {
            Assert.AreEqual(-1, ReadIndex.GetLastStreamEventNumber("EF"));
        }

        [Test]
        public void not_find_log_record_for_nonexistent_stream_with_same_hash()
        {
            EventRecord prepare;
            Assert.AreEqual(SingleReadResult.NoStream, ReadIndex.ReadEvent("EF", 0, out prepare));
        }

        [Test]
        public void not_return_range_for_non_existing_stream_with_same_hash()
        {
            EventRecord[] records;
            Assert.AreEqual(RangeReadResult.NoStream, ReadIndex.ReadStreamEventsBackward("EF", 0, 1, out records));
            Assert.AreEqual(0, records.Length);
        }
    }
}