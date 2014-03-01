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
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.v8
{
    [TestFixture]
    public class when_running_a_projection_with_created_handler : TestFixtureWithJsProjection
    {
        protected override void Given()
        {
            _projection = @"
                fromAll().foreachStream().when({
                    $created: function(s, e) {
                        log('handler-invoked');
                        log(e.streamId);
                        log(e.eventType);
                        log(e.body);
                        log(e.metadata);
                        emit('stream1', 'event1', {a:1});
                    }   
                });
            ";
            _state = @"{}";
        }

        [Test, Category("v8")]
        public void invokes_created_handler()
        {

            var e = new ResolvedEvent(
                "stream", 0, "stream", 0, false, new TFPos(1000, 900), Guid.NewGuid(), "event", true, "{}", "{\"m\":1}");
            
            EmittedEventEnvelope[] emittedEvents;
            _stateHandler.ProcessPartitionCreated(
                "partition", CheckpointTag.FromPosition(0, 10, 5), e, out emittedEvents);

            Assert.AreEqual(5, _logged.Count);
            Assert.AreEqual(@"handler-invoked", _logged[0]);
            Assert.AreEqual(@"stream", _logged[1]);
            Assert.AreEqual(@"event", _logged[2]);
            Assert.AreEqual(@"{}", _logged[3]);
            Assert.AreEqual(@"{""m"":1}", _logged[4]);
        }

        [Test, Category("v8")]
        public void returns_emitted_events()
        {

            var e = new ResolvedEvent(
                "stream", 0, "stream", 0, false, new TFPos(1000, 900), Guid.NewGuid(), "event", true, "{}", "{\"m\":1}");

            EmittedEventEnvelope[] emittedEvents;
            _stateHandler.ProcessPartitionCreated(
                "partition", CheckpointTag.FromPosition(0, 10, 5), e, out emittedEvents);

            Assert.IsNotNull(emittedEvents);
            Assert.AreEqual(1, emittedEvents.Length);
            Assert.AreEqual("stream1", emittedEvents[0].Event.StreamId);
            Assert.AreEqual("event1", emittedEvents[0].Event.EventType);
            Assert.AreEqual("{\"a\":1}", emittedEvents[0].Event.Data);
        }
    }
}
