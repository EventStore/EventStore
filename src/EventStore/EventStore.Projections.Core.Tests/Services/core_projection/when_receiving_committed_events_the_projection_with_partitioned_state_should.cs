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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_receiving_committed_events_the_projection_with_partitioned_state_should :
        TestFixtureWithCoreProjection
    {
        private Guid _eventId;

        protected override void Given()
        {
            _configureBuilderByQuerySource = source =>
                {
                    source.FromAll();
                    source.AllEvents();
                    source.SetByStream();
                };
            TicksAreHandledImmediately();
            NoStream("$projections-projection-state");
            NoStream("$projections-projection-checkpoint");
            NoStream("$projections-projection-account-01-state");
            NoStream("$projections-projection-account-02-state");
            AllWritesSucceed();
        }

        protected override void When()
        {
            //projection subscribes here
            _eventId = Guid.NewGuid();
            _consumer.HandledMessages.Clear();
            _coreProjection.Handle(
                ProjectionMessage.SubscriptionMessage.CommittedEventReceived.Sample(Guid.Empty, new EventPosition(120, 110), "account-01", 1, false,
                       new Event(
                           _eventId, "handle_this_type", false, Encoding.UTF8.GetBytes("data1"),
                           Encoding.UTF8.GetBytes("metadata")), 0));
            _coreProjection.Handle(
                ProjectionMessage.SubscriptionMessage.CommittedEventReceived.Sample(Guid.Empty, new EventPosition(140, 130), "account-02", 2, false,
                       new Event(
                           _eventId, "handle_this_type", false, Encoding.UTF8.GetBytes("data2"),
                           Encoding.UTF8.GetBytes("metadata")), 1));
            _coreProjection.Handle(
                ProjectionMessage.SubscriptionMessage.CommittedEventReceived.Sample(Guid.Empty, new EventPosition(160, 150), "account-01", 2, false,
                       new Event(
                           _eventId, "append", false, Encoding.UTF8.GetBytes("$"), Encoding.UTF8.GetBytes("metadata")), 2));
            _coreProjection.Handle(
                ProjectionMessage.SubscriptionMessage.CommittedEventReceived.Sample(Guid.Empty, new EventPosition(180, 170), "account-02", 3, false,
                       new Event(
                           _eventId, "append", false, Encoding.UTF8.GetBytes("$"), Encoding.UTF8.GetBytes("metadata")), 3));
        }

        [Test]
        public void request_partition_state_from_the_correct_stream()
        {
            // 1 - for load state
            // 2 - by emitted stream to ensure idempotency
            Assert.AreEqual(
                3,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>().Count(
                    v => v.EventStreamId == "$projections-projection-account-01-state"));
            Assert.AreEqual(
                3,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>().Count(
                    v => v.EventStreamId == "$projections-projection-account-02-state"));
        }

        [Test]
        public void update_state_snapshots_are_written_to_the_correct_stream()
        {
            Assert.AreEqual(4, _writeEventHandler.HandledMessages.Count);
            Assert.AreEqual(
                "$projections-projection-account-01-state", _writeEventHandler.HandledMessages[0].EventStreamId);
            Assert.AreEqual(
                "$projections-projection-account-02-state", _writeEventHandler.HandledMessages[1].EventStreamId);
            Assert.AreEqual(
                "$projections-projection-account-01-state", _writeEventHandler.HandledMessages[2].EventStreamId);
            Assert.AreEqual(
                "$projections-projection-account-02-state", _writeEventHandler.HandledMessages[3].EventStreamId);
        }

        [Test]
        public void update_state_snapshots_are_correct()
        {
            Assert.AreEqual(4, _writeEventHandler.HandledMessages.Count);
            Assert.AreEqual("data1", Encoding.UTF8.GetString(_writeEventHandler.HandledMessages[0].Events[0].Data));
            Assert.AreEqual("data2", Encoding.UTF8.GetString(_writeEventHandler.HandledMessages[1].Events[0].Data));
            Assert.AreEqual("data1$", Encoding.UTF8.GetString(_writeEventHandler.HandledMessages[2].Events[0].Data));
            Assert.AreEqual("data2$", Encoding.UTF8.GetString(_writeEventHandler.HandledMessages[3].Events[0].Data));
        }
    }
}
