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
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_receiving_a_committed_event_the_projection_with_partitioned_state_by_custom_rule_should :
        TestFixtureWithCoreProjectionStarted
    {
        private Guid _eventId;

        protected override void Given()
        {
            _configureBuilderByQuerySource = source =>
                {
                    source.FromAll();
                    source.AllEvents();
                    source.SetByCustomPartitions();
                };
            TicksAreHandledImmediately();
            NoStream("$projections-projection-order");
            AllWritesToSucceed("$projections-projection-order");
            NoStream("$projections-projection-checkpoint");
            NoStream("$projections-projection-region-a-checkpoint");

            _stateHandler = new FakeProjectionStateHandler(configureBuilder: _configureBuilderByQuerySource, failOnGetPartition: false);

        }

        protected override void When()
        {
            //projection subscribes here
            _eventId = Guid.NewGuid();
            _consumer.HandledMessages.Clear();
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    Guid.Empty, _subscriptionId, new EventPosition(120, 110), "account-01", -1, false,
                    ResolvedEvent.Sample(
                        _eventId, "handle_this_type", false, Encoding.UTF8.GetBytes("data"),
                        Encoding.UTF8.GetBytes("metadata")), 0));
        }

        [Test]
        public void request_partition_state_from_the_correct_stream()
        {
            Assert.AreEqual(
                1,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>()
                         .Count(v => v.EventStreamId == "$projections-projection-region-a-checkpoint"));
        }

        [Test]
        public void update_state_snapshot_is_written_to_the_correct_stream()
        {
            Assert.AreEqual(1, _writeEventHandler.HandledMessages.OfEventType("StateUpdated").Count);
            var message = _writeEventHandler.HandledMessages.WithEventType("StateUpdated")[0];
            Assert.AreEqual("$projections-projection-region-a-state", message.EventStreamId);
        }

        [Test]
        public void pass_partition_name_to_state_handler()
        {
            Assert.AreEqual("region-a", _stateHandler._lastPartition);
        }
    }
}
