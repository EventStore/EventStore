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
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning")]
    public class read_event_should: SpecificationWithMiniNode
    {
        private Guid _eventId0;
        private Guid _eventId1;

        protected override void When()
        {
            _eventId0 = Guid.NewGuid();
            _eventId1 = Guid.NewGuid();

            _conn.AppendToStream("test-stream",
                                 -1, 
                                 new EventData(_eventId0, "event0", false, new byte[3], new byte[2]),
                                 new EventData(_eventId1, "event1", false, new byte[7], new byte[10]));
            _conn.DeleteStream("deleted-stream", -1, hardDelete: true);
        }

        [Test, Category("Network")]
        public void throw_if_stream_id_is_null()
        {
            Assert.Throws<ArgumentNullException>(() => _conn.ReadEventAsync(null, 0, resolveLinkTos: false));
        }

        [Test, Category("Network")]
        public void throw_if_stream_id_is_empty()
        {
            Assert.Throws<ArgumentNullException>(() => _conn.ReadEventAsync("", 0, resolveLinkTos: false));
        }

        [Test, Category("Network")]
        public void throw_if_event_number_is_less_than_minus_one()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _conn.ReadEventAsync("stream", -2, resolveLinkTos: false));
        }

        [Test, Category("Network")]
        public void notify_using_status_code_if_stream_not_found()
        {
            var res = _conn.ReadEvent("unexisting-stream", 5, false);

            Assert.AreEqual(EventReadStatus.NoStream, res.Status);
            Assert.IsNull(res.Event);
            Assert.AreEqual("unexisting-stream", res.Stream);
            Assert.AreEqual(5, res.EventNumber);
        }

        [Test, Category("Network")]
        public void return_no_stream_if_requested_last_event_in_empty_stream()
        {
            var res = _conn.ReadEvent("some-really-empty-stream", -1, false);
            Assert.AreEqual(EventReadStatus.NoStream, res.Status);
        }

        [Test, Category("Network")]
        public void notify_using_status_code_if_stream_was_deleted()
        {
            var res = _conn.ReadEvent("deleted-stream", 5, false);

            Assert.AreEqual(EventReadStatus.StreamDeleted, res.Status);
            Assert.IsNull(res.Event);
            Assert.AreEqual("deleted-stream", res.Stream);
            Assert.AreEqual(5, res.EventNumber);
        }

        [Test, Category("Network")]
        public void notify_using_status_code_if_stream_does_not_have_event()
        {
            var res = _conn.ReadEvent("test-stream", 5, false);

            Assert.AreEqual(EventReadStatus.NotFound, res.Status);
            Assert.IsNull(res.Event);
            Assert.AreEqual("test-stream", res.Stream);
            Assert.AreEqual(5, res.EventNumber);
        }

        [Test, Category("Network")]
        public void return_existing_event()
        {
            var res = _conn.ReadEvent("test-stream", 0, false);

            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(res.Event.Value.OriginalEvent.EventId, _eventId0);
            Assert.AreEqual("test-stream", res.Stream);
            Assert.AreEqual(0, res.EventNumber);
            Assert.AreNotEqual(DateTime.MinValue, res.Event.Value.OriginalEvent.Created);
        }

        [Test, Category("Network")]
        public void return_last_event_in_stream_if_event_number_is_minus_one()
        {
            var res = _conn.ReadEvent("test-stream", -1, false);

            Assert.AreEqual(EventReadStatus.Success, res.Status);
            Assert.AreEqual(res.Event.Value.OriginalEvent.EventId, _eventId1);
            Assert.AreEqual("test-stream", res.Stream);
            Assert.AreEqual(-1, res.EventNumber);
            Assert.AreNotEqual(DateTime.MinValue, res.Event.Value.OriginalEvent.Created);
        }
    }
}
