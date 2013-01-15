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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.v8
{
    [TestFixture]
    public class when_running_reflecting_v8_projection : TestFixtureWithJsProjection
    {
        protected override void Given()
        {
            _projection = @"
                fromAll();
                on_raw(function(state, event) {
                    log(JSON.stringify(state) + '/' + event.bodyRaw + '/' + event.streamId + '/' + 
                        event.eventType + '/' + event.sequenceNumber + '/' + event.metadataRaw + '/' + 
                        JSON.parse(event.position).commitPosition + '/' + JSON.parse(event.position).preparePosition);
                    return {};
                });
            ";
        }

        [Test, Category("v8")]
        public void process_event_should_reflect_event()
        {
            string state;
            EmittedEvent[] emittedEvents;
            _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
                @"{""a"":""b""}", out state, out emittedEvents);
            Assert.AreEqual(1, _logged.Count);
            Assert.AreEqual(@"{}/{""a"":""b""}/stream1/type1/0/metadata/20/10", _logged[0]);
        }

        [Test, Category("v8")]
        public void multiple_process_event_should_reflect_events()
        {
            string state;
            EmittedEvent[] emittedEvents;
            _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
                @"{""a"":""b""}", out state, out emittedEvents);
            _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(40, 30), "stream1", "type1", "category", Guid.NewGuid(), 1, "metadata",
                @"{""c"":""d""}", out state, out emittedEvents);
            Assert.AreEqual(2, _logged.Count);
            Assert.AreEqual(@"{}/{""a"":""b""}/stream1/type1/0/metadata/20/10", _logged[0]);
            Assert.AreEqual(@"{}/{""c"":""d""}/stream1/type1/1/metadata/40/30", _logged[1]);
        }

        [Test, Category("v8")]
        public void process_event_returns_true()
        {
            string state;
            EmittedEvent[] emittedEvents;
            var result = _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(20, 10), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
                @"{""a"":""b""}", out state, out emittedEvents);

            Assert.IsTrue(result);
        }

        [Test, Category("v8")]
        public void process_event_with_null_category_returns_true()
        {
            string state;
            EmittedEvent[] emittedEvents;
            var result = _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(20, 10), "stream1", "type1", null, Guid.NewGuid(), 0, "metadata", @"{""a"":""b""}",
                out state, out emittedEvents);

            Assert.IsTrue(result);
        }
    }
}
