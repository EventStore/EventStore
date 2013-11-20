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
using System.Text;
using EventStore.Core.Data;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_receiving_a_committed_event_the_projection_should : TestFixtureWithCoreProjectionStarted
    {
        private Guid _eventId;

        protected override void Given()
        {
            TicksAreHandledImmediately();
            AllWritesSucceed();
            NoOtherStreams();
        }

        protected override void When()
        {
            //projection subscribes here
            _eventId = Guid.NewGuid();
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "/event_category/1", -1, "/event_category/1", -1, false, new TFPos(120, 110), _eventId,
                        "handle_this_type", false, "data", "metadata"), _subscriptionId, 0));
        }

        [Test]
        public void update_state_snapshot_at_correct_position()
        {
            Assert.AreEqual(1, _writeEventHandler.HandledMessages.OfEventType("Result").Count);

            var metedata =
                _writeEventHandler.HandledMessages.OfEventType("Result")[0].Metadata.ParseCheckpointTagVersionExtraJson(default(ProjectionVersion));

            Assert.AreEqual(120, metedata.Tag.CommitPosition);
            Assert.AreEqual(110, metedata.Tag.PreparePosition);
        }

        [Test]
        public void pass_event_to_state_handler()
        {
            Assert.AreEqual(1, _stateHandler._eventsProcessed);
            Assert.AreEqual("/event_category/1", _stateHandler._lastProcessedStreamId);
            Assert.AreEqual("handle_this_type", _stateHandler._lastProcessedEventType);
            Assert.AreEqual(_eventId, _stateHandler._lastProcessedEventId);
            //TODO: support sequence numbers here
            Assert.AreEqual("metadata", _stateHandler._lastProcessedMetadata);
            Assert.AreEqual("data", _stateHandler._lastProcessedData);
        }
    }
}
