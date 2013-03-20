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
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.Scavenge
{
    [TestFixture]
    public class when_deleting_single_stream_spanning_through_2_chunks_in_db_with_2_chunks :ReadIndexTestScenario
    {
        private EventRecord _event1;
        private EventRecord _event5;
        private EventRecord _event6;
        private EventRecord _delete;

        protected override void WriteTestScenario()
        {
            _event1 = WriteStreamCreated("ES");

            WriteSingleEvent("ES", 1, new string('.', 3000));
            WriteSingleEvent("ES", 2, new string('.', 3000));
            WriteSingleEvent("ES", 3, new string('.', 3000));
            
            _event5 = WriteSingleEvent("ES", 4, new string('.', 3000), retryOnFail: true); // chunk 2
            _event6 = WriteSingleEvent("ES", 5, new string('.', 3000));

            _delete = WriteDelete("ES");
            Scavenge(completeLast: false, mergeChunks: false);
        }

        [Test]
        public void read_all_forward_returns_events_only_from_uncompleted_chunk_and_delete_record()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(4, events.Length);
            Assert.AreEqual(_event1, events[0]);
            Assert.AreEqual(_event5, events[1]);
            Assert.AreEqual(_event6, events[2]);
            Assert.AreEqual(_delete, events[3]);
        }

        [Test]
        public void read_all_backward_returns_events_only_from_uncompleted_chunk_and_delete_record()
        {
            var events = ReadIndex.ReadAllEventsBackward(GetBackwardReadPos(), 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(4, events.Length);
            Assert.AreEqual(_event1, events[3]);
            Assert.AreEqual(_event5, events[2]);
            Assert.AreEqual(_event6, events[1]);
            Assert.AreEqual(_delete, events[0]);
        }

        [Test]
        public void read_all_backward_from_beginning_of_second_chunk_returns_no_records()
        {
            var pos = new TFPos(10000, 10000);
            var events = ReadIndex.ReadAllEventsBackward(pos, 100).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(_event1, events[0]);
        }

        [Test]
        public void read_all_forward_from_beginning_of_second_chunk_with_max_1_record_returns_5th_record()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(10000, 10000), 1).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(1, events.Length);
            Assert.AreEqual(_event5, events[0]);
        }

        [Test]
        public void read_all_forward_with_max_5_records_returns_3_records_from_second_chunk_and_delete_record()
        {
            var events = ReadIndex.ReadAllEventsForward(new TFPos(0,0), 5).Records.Select(r => r.Event).ToArray();
            Assert.AreEqual(4, events.Length);
            Assert.AreEqual(_event1, events[0]);
            Assert.AreEqual(_event5, events[1]);
            Assert.AreEqual(_event6, events[2]);
            Assert.AreEqual(_delete, events[3]);
        }

        [Test]
        public void is_stream_deleted_returns_true()
        {
            Assert.That(ReadIndex.IsStreamDeleted("ES"));
        }

        [Test]
        public void last_event_number_returns_stream_deleted()
        {
            Assert.AreEqual(EventNumber.DeletedStream, ReadIndex.GetLastStreamEventNumber("ES"));
        }

    }
}
