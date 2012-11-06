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
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.ReadAll
{
    [TestFixture]
    class with_single_deleted_stream_read_index_should : ReadIndexTestScenario
    {
        private EventRecord _event;
        private EventRecord _delete;

        protected override void WriteTestScenario()
        {
            _event = WriteSingleEvent("ES", 0, "test1");
            _delete = WriteDelete("ES");
        }

        [Test]
        public void return_deleted_events_but_exclude_delete_event_when_reading_all_forward()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
            Assert.AreEqual(1, records.Count);
            Assert.That(records[0].Event.Equals(_event));
        }

        [Test]
        public void return_deleted_events_but_exclude_delete_event_when_reading_all_backward()
        {
            var records = ReadIndex.ReadAllEventsForward(new TFPos(0, 0), 100).Records;
            Assert.AreEqual(1, records.Count);
            Assert.That(records[0].Event.Equals(_event));
        }

    }
}
