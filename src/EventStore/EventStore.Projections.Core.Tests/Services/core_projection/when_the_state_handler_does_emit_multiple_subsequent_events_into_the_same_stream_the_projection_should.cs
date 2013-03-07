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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_the_state_handler_does_emit_multiple_subsequent_events_into_the_same_stream_the_projection_should :
        TestFixtureWithCoreProjectionStarted
    {
        protected override void Given()
        {
            ExistingEvent(
                "$projections-projection-result", "Result", @"{""commitPosition"": 100, ""preparePosition"": 50}", "{}");
            ExistingEvent(
                "$projections-projection-checkpoint", "ProjectionCheckpoint",
                @"{""commitPosition"": 100, ""preparePosition"": 50}", "{}");
            NoStream(FakeProjectionStateHandler._emit2StreamId);
            NoStream("$projections-projection-order");
            AllWritesToSucceed("$projections-projection-order");
        }

        protected override void When()
        {
            //projection subscribes here
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "/event_category/1", -1, "/event_category/1", -1, false, new EventPosition(120, 110),
                        Guid.NewGuid(), "emit22_type", false, Encoding.UTF8.GetBytes("data"),
                        Encoding.UTF8.GetBytes("metadata"), default(DateTime)), Guid.Empty, _subscriptionId, 0));
        }


        [Test]
        public void write_events_in_a_single_transaction()
        {
            Assert.IsTrue(_writeEventHandler.HandledMessages.Any(v => v.Events.Length == 2));
        }

        [Test]
        public void write_all_the_emitted_events()
        {
            Assert.AreEqual(
                2, _writeEventHandler.HandledMessages.Single(v => v.EventStreamId == "/emit2").Events.Length);
        }

        [Test]
        public void write_events_in_correct_order()
        {
            Assert.AreEqual(
                FakeProjectionStateHandler._emit1Data,
                Encoding.UTF8.GetString(
                    _writeEventHandler.HandledMessages.Single(v => v.EventStreamId == "/emit2").Events[0].Data));
            Assert.AreEqual(
                FakeProjectionStateHandler._emit2Data,
                Encoding.UTF8.GetString(
                    _writeEventHandler.HandledMessages.Single(v => v.EventStreamId == "/emit2").Events[1].Data));
        }
    }
}
