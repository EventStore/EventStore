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
using System.Globalization;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.v8
{
    [TestFixture]
    public class when_running_counting_v8_projection : TestFixtureWithJsProjection
    {
        protected override void Given()
        {
            _projection = @"
                fromAll().when({$any: 
                    function(state, event) {
                        state.count = state.count + 1;
                        log(state.count);
                        return state;
                    }});
            ";
            _state = @"{""count"": 0}";
        }

        [Test, Category("v8")]
        public void process_event_counts_events()
        {
            string state;
            EmittedEventEnvelope[] emittedEvents;
            _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(10, 5), "stream1", "type1", "category", Guid.NewGuid(), 0, "metadata",
                @"{""a"":""b""}", out state, out emittedEvents);
            _stateHandler.ProcessEvent(
                "", CheckpointTag.FromPosition(20, 15), "stream1", "type1", "category", Guid.NewGuid(), 1, "metadata",
                @"{""a"":""b""}", out state, out emittedEvents);
            Assert.AreEqual(2, _logged.Count);
            Assert.AreEqual(@"1", _logged[0]);
            Assert.AreEqual(@"2", _logged[1]);
        }

        [Test, Category("v8"), Category("Manual"), Ignore]
        public void can_handle_million_events()
        {
            for (var i = 0; i < 1000000; i++)
            {
                _logged.Clear();
                string state;
                EmittedEventEnvelope[] emittedEvents;
                _stateHandler.ProcessEvent(
                    "", CheckpointTag.FromPosition(i * 10, i * 10 - 5), "stream" + i, "type" + i, "category", Guid.NewGuid(), 0,
                    "metadata", @"{""a"":""" + i + @"""}", out state, out emittedEvents);
                Assert.AreEqual(1, _logged.Count);
                Assert.AreEqual((i + 1).ToString(CultureInfo.InvariantCulture), _logged[_logged.Count - 1]);
            }
        }
    }
}
