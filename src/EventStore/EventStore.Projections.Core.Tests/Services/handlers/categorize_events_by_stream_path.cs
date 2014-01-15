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
using EventStore.Projections.Core.Standard;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.handlers
{
    public static class categorize_events_by_stream_path
    {
        [TestFixture]
        public class when_handling_simple_event
        {
            private CategorizeEventsByStreamPath _handler;
            private string _state;
            private string _sharedState;
            private EmittedEventEnvelope[] _emittedEvents;
            private bool _result;

            [SetUp]
            public void when()
            {
                _handler = new CategorizeEventsByStreamPath("-", Console.WriteLine);
                _handler.Initialize();
                _result = _handler.ProcessEvent(
                    "", CheckpointTag.FromPosition(0, 200, 150), null,
                    new ResolvedEvent(
                        "cat1-stream1", 10, "cat1-stream1", 10, false, new TFPos(200, 150), Guid.NewGuid(),
                        "event_type", true, "{}", "{}"), out _state, out _sharedState, out _emittedEvents);
            }

            [Test]
            public void result_is_true()
            {
                Assert.IsTrue(_result);
            }

            [Test]
            public void state_stays_null()
            {
                Assert.IsNull(_state);
            }

            [Test]
            public void emits_correct_link()
            {
                Assert.NotNull(_emittedEvents);
                Assert.AreEqual(1, _emittedEvents.Length);
                var @event = _emittedEvents[0].Event;
                Assert.AreEqual("$>", @event.EventType);
                Assert.AreEqual("$ce-cat1", @event.StreamId);
                Assert.AreEqual("10@cat1-stream1", @event.Data);
            }

        }

        [TestFixture]
        public class when_handling_link_to_event
        {
            private CategorizeEventsByStreamPath _handler;
            private string _state;
            private string _sharedState;
            private EmittedEventEnvelope[] _emittedEvents;
            private bool _result;

            [SetUp]
            public void when()
            {
                _handler = new CategorizeEventsByStreamPath("-", Console.WriteLine);
                _handler.Initialize();
                _result = _handler.ProcessEvent(
                    "", CheckpointTag.FromPosition(0, 200, 150), null,
                    new ResolvedEvent(
                        "cat2-stream2", 20, "cat2-stream2", 20, true, new TFPos(200, 150), Guid.NewGuid(),
                        "$>", true, "10@cat1-stream1", "{}"), out _state, out _sharedState, out _emittedEvents);
            }

            [Test]
            public void result_is_true()
            {
                Assert.IsTrue(_result);
            }

            [Test]
            public void state_stays_null()
            {
                Assert.IsNull(_state);
            }

            [Test]
            public void emits_correct_link()
            {
                Assert.NotNull(_emittedEvents);
                Assert.AreEqual(1, _emittedEvents.Length);
                var @event = _emittedEvents[0].Event;
                Assert.AreEqual("$>", @event.EventType);
                Assert.AreEqual("$ce-cat2", @event.StreamId);
                Assert.AreEqual("10@cat1-stream1", @event.Data);
            }

        }
    }
}
