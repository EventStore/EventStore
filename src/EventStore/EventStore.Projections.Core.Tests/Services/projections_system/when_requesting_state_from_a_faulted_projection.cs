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

using System.Collections.Generic;
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.projections_system
{
    [TestFixture]
    class when_requesting_state_from_a_faulted_projection : with_projection_config
    {
        private TFPos _message1Position;

        protected override void Given()
        {
            base.Given();
            NoOtherStreams();
            _message1Position = ExistingEvent("stream1", "message1", null, "{}");

            _projectionSource = @"fromAll().when({message1: function(s,e){ throw 1; }});";
        }

        protected override IEnumerable<WhenStep> When()
        {
            yield return
                new ProjectionManagementMessage.Post(
                    Envelope, ProjectionMode.Continuous, _projectionName, ProjectionManagementMessage.RunAs.System, "js",
                    _projectionSource, enabled: true, checkpointsEnabled: true, emitEnabled: true);
            yield return Yield;
            yield return new ProjectionManagementMessage.GetState(Envelope, _projectionName, "");
        }

        protected override bool GivenStartSystemProjections()
        {
            return true;
        }

        [Test]
        public void reported_state_is_before_the_fault_position()
        {
            var states = HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().ToArray();
            Assert.AreEqual(1, states.Length);
            var state = states[0];

            Assert.That(state.Position.Streams.Count == 1);
            Assert.That(state.Position.Streams.Keys.First() == "message1");
            Assert.That(state.Position.Streams["message1"] == -1);
            Assert.That(
                state.Position.Position <= _message1Position, "{0} <= {1}", state.Position.Position, _message1Position);
        }
    }
}
