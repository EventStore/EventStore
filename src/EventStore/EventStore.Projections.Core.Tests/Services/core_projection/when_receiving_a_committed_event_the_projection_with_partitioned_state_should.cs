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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_receiving_a_committed_event_the_projection_with_partitioned_state_should :
        TestFixtureWithCoreProjectionStarted
    {
        private Guid _eventId;

        protected override void Given()
        {
            _configureBuilderByQuerySource = source =>
                {
                    source.FromAll();
                    source.AllEvents();
                    source.SetByStream();
                    source.SetEmitStateUpdated();
                };
            TicksAreHandledImmediately();
            NoStream("$projections-projection-state");
            NoStream("$projections-projection-order");
            AllWritesToSucceed("$projections-projection-order");
            NoStream("$projections-projection-checkpoint");
            NoStream("$projections-projection-partitions");
            NoStream("$projections-projection-account-01-state");
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
            // 1 - for load state
            // 2 - by emitted stream to ensure idempotency
            Assert.AreEqual(
                2,
                _consumer.HandledMessages.OfType<ClientMessage.ReadStreamEventsBackward>().Count(
                    v => v.EventStreamId == "$projections-projection-account-01-state"));
        }

        [Test]
        public void update_state_snapshot_is_written_to_the_correct_stream()
        {
            var writeEvents =
                _writeEventHandler.HandledMessages.Where(v => v.Events.Any(e => e.EventType == "StateUpdated")).ToList();
            Assert.AreEqual(1, writeEvents.Count);

            var message = writeEvents[0];
            Assert.AreEqual("$projections-projection-account-01-state", message.EventStreamId);
        }

        [Test]
        public void update_state_snapshot_at_correct_position()
        {
            var writeEvents =
                _writeEventHandler.HandledMessages.Where(v => v.Events.Any(e => e.EventType == "StateUpdated")).ToList();
            Assert.AreEqual(1, writeEvents.Count);

            var metedata = writeEvents[0].Events[0].Metadata.ParseJson<CheckpointTag>();

            Assert.AreEqual(120, metedata.CommitPosition);
            Assert.AreEqual(110, metedata.PreparePosition);
        }

        [Test]
        public void pass_event_to_state_handler()
        {
            Assert.AreEqual(1, _stateHandler._eventsProcessed);
            Assert.AreEqual("account-01", _stateHandler._lastProcessedStreamId);
            Assert.AreEqual("handle_this_type", _stateHandler._lastProcessedEventType);
            Assert.AreEqual(_eventId, _stateHandler._lastProcessedEventId);
            //TODO: support sequence numbers here
            Assert.AreEqual("metadata", _stateHandler._lastProcessedMetadata);
            Assert.AreEqual("data", _stateHandler._lastProcessedData);
        }

        [Test]
        public void register_new_partition_state_stream()
        {
            var writes =
                _writeEventHandler.HandledMessages.Where(v => v.EventStreamId == "$projections-projection-partitions")
                                  .ToArray();
            Assert.AreEqual(1, writes.Length);
            var write = writes[0];

            Assert.AreEqual(1, write.Events.Length);

            var @event = write.Events[0];

            Assert.AreEqual("account-01", Encoding.UTF8.GetString(@event.Data));
        }


    }
}
