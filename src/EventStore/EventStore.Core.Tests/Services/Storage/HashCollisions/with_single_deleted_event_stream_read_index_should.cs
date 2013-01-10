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

using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.Storage.ReaderIndex;
using NUnit.Framework;
using ReadStreamResult = EventStore.Core.Services.Storage.ReaderIndex.ReadStreamResult;

namespace EventStore.Core.Tests.Services.Storage.HashCollisions
{
    [TestFixture]
    public class with_single_deleted_event_stream_read_index_should : ReadIndexTestScenario
    {
        private EventRecord _prepare1;
        private EventRecord _delete1;

        protected override void WriteTestScenario()
        {
            _prepare1 = WriteSingleEvent("ES", 0, "test1");
            _delete1 = WriteDelete("ES");
        }

        [Test]
        public void return_minus_one_for_nonexistent_stream_as_last_event_version()
        {
            Assert.AreEqual(-1, ReadIndex.GetLastStreamEventNumber("ES-NONEXISTENT"));
        }

        [Test]
        public void return_empty_range_on_from_start_range_query_for_non_existing_stream()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES-NONEXISTING", 0, 1);
            Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
            Assert.AreEqual(0, result.Records.Length);
        }

        [Test]
        public void return_empty_range_on_from_end_range_query_for_non_existing_stream()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES-NONEXISTING", 0, 1);
            Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
            Assert.AreEqual(0, result.Records.Length);
        }

        [Test]
        public void return_not_found_for_get_record_from_non_existing_stream()
        {
            var result = ReadIndex.ReadEvent("ES-NONEXISTING", 0);
            Assert.AreEqual(ReadEventResult.NoStream, result.Result);
            Assert.IsNull(result.Record);
        }

        [Test]
        public void return_correct_event_version_for_deleted_stream()
        {
            Assert.AreEqual(EventNumber.DeletedStream, ReadIndex.GetLastStreamEventNumber("ES"));
        }

        [Test]
        public void return_stream_deleted_result_for_deleted_event_stream()
        {
            var result = ReadIndex.ReadEvent("ES", 0);
            Assert.AreEqual(ReadEventResult.StreamDeleted, result.Result);
            Assert.IsNull(result.Record);
        }

        [Test]
        public void return_empty_range_on_from_start_range_query_for_deleted_event_stream()
        {
            var result = ReadIndex.ReadStreamEventsForward("ES", 0, 1);
            Assert.AreEqual(ReadStreamResult.StreamDeleted, result.Result);
            Assert.AreEqual(0, result.Records.Length);
        }

        [Test]
        public void return_empty_range_on_from_end_range_query_for_deleted_event_stream()
        {
            var result = ReadIndex.ReadStreamEventsBackward("ES", 0, 1);
            Assert.AreEqual(ReadStreamResult.StreamDeleted, result.Result);
            Assert.AreEqual(0, result.Records.Length);
        }

        [Test]
        public void return_correct_last_event_version_for_nonexistent_stream_with_same_hash_as_deleted_one()
        {
            Assert.AreEqual(-1, ReadIndex.GetLastStreamEventNumber("AB"));
        }

        [Test]
        public void not_find_record_for_nonexistent_event_stream_with_same_hash_as_deleted_one()
        {
            var result = ReadIndex.ReadEvent("AB", 0);
            Assert.AreEqual(ReadEventResult.NoStream, result.Result);
            Assert.IsNull(result.Record);
        }

        [Test]
        public void return_empty_range_on_from_start_query_for_nonexisting_event_stream_with_same_hash_as_deleted_one()
        {
            var result = ReadIndex.ReadStreamEventsForward("HG", 0, 1);
            Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
            Assert.AreEqual(0, result.Records.Length);
        }

        [Test]
        public void return_empty_on_from_end_query_for_nonexisting_event_stream_with_same_hash_as_deleted_one()
        {
            var result = ReadIndex.ReadStreamEventsBackward("HG", 0, 1);
            Assert.AreEqual(ReadStreamResult.NoStream, result.Result);
            Assert.AreEqual(0, result.Records.Length);
        }

        [Test]
        public void not_find_record_with_nonexistent_version_for_deleted_event_stream()
        {
            var result = ReadIndex.ReadEvent("ES", 1);
            Assert.AreEqual(ReadEventResult.StreamDeleted, result.Result);
            Assert.IsNull(result.Record);
        }

        [Test]
        public void not_find_record_with_non_existing_version_for_event_stream_with_same_hash_as_deleted_one()
        {
            var result = ReadIndex.ReadEvent("CL", 1);
            Assert.AreEqual(ReadEventResult.NoStream, result.Result);
            Assert.IsNull(result.Record);
        }

        [Test]
        public void return_all_events_on_read_all_forward()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(_prepare1, events[0]);
            Assert.AreEqual(_delete1, events[1]);
        }

        [Test]
        public void return_all_events_on_read_all_backward()
        {
            var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(2, events.Length);
            Assert.AreEqual(_delete1, events[0]);
            Assert.AreEqual(_prepare1, events[1]);
        }
    }
}