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
using System.Linq;
using System.Text;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_the_state_handler_does_emit_an_event_the_projection_should : TestFixtureWithCoreProjectionStarted
    {
        private Guid _causingEventId;

        protected override void Given()
        {
            NoStream("$projections-projection-result");
            NoStream("$projections-projection-order");
            AllWritesToSucceed("$projections-projection-order");
            NoStream("$projections-projection-checkpoint");
            NoStream(FakeProjectionStateHandler._emit1StreamId);
        }

        protected override void When()
        {
            //projection subscribes here
            _causingEventId = Guid.NewGuid();
            var committedEventReceived =
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "/event_category/1", -1, "/event_category/1", -1, false, new EventPosition(120, 110),
                        _causingEventId, "no_state_emit1_type", false, "data",
                        "metadata"), _subscriptionId, 0);
            _coreProjection.Handle(committedEventReceived);
        }

        [Test]
        public void write_the_emitted_event()
        {
            Assert.IsTrue(
                _writeEventHandler.HandledMessages.Any(
                    v => Encoding.UTF8.GetString(v.Events[0].Data) == FakeProjectionStateHandler._emit1Data));
        }

        [Test]
        public void set_a_caused_by_position_attributes()
        {
            var metadata = _writeEventHandler.HandledMessages[0].Events[0].Metadata.ParseCheckpointTagJson(default(ProjectionVersion));
            Assert.AreEqual(120, metadata.Tag.CommitPosition);
            Assert.AreEqual(110, metadata.Tag.PreparePosition);
        }
    }
}
