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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    public abstract class specification_with_query_without_when : TestFixtureWithCoreProjectionStarted
    {
        protected Guid _eventId;

        protected override bool GivenCheckpointsEnabled()
        {
            return false;
        }

        protected override bool GivenEmitEventEnabled()
        {
            return false;
        }

        protected override bool GivenStopOnEof()
        {
            return true;
        }

        protected override int GivenPendingEventsThreshold()
        {
            return 0;
        }

        protected override ProjectionProcessingStrategy GivenProjectionProcessingStrategy()
        {
            return CreateQueryProcessingStrategy();
        }

        protected override void Given()
        {
            _checkpointHandledThreshold = 0;
            _checkpointUnhandledBytesThreshold = 0;
            _configureBuilderByQuerySource = source =>
            {
                source.FromAll();
                source.AllEvents();
                source.NoWhen();
            };
            TicksAreHandledImmediately();
            NoOtherStreams();
            AllWritesSucceed();
        }
    }

    [TestFixture]
    public class when_receiving_committed_events_the_projection_without_when : specification_with_query_without_when
    {
        protected override void When()
        {
            //projection subscribes here
            _eventId = Guid.NewGuid();
            _consumer.HandledMessages.Clear();
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", 1, "account-01", 1, false, new TFPos(120, 110), _eventId, "handle_this_type",
                        false, "data1", "metadata"), _subscriptionId, 0));
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-02", 2, "account-02", 2, false, new TFPos(140, 130), _eventId, "handle_this_type",
                        false, "data2", "metadata"), _subscriptionId, 1));
        }

        [Test]
        public void update_state_snapshots_are_written_to_the_correct_stream()
        {
            var writeEvents =
                _writeEventHandler.HandledMessages.Where(v => v.Events.Any(e => e.EventType == "Result")).ToList();
            Assert.AreEqual(2, writeEvents.Count);
            Assert.AreEqual("$projections-projection-result", writeEvents[0].EventStreamId);
            Assert.AreEqual("$projections-projection-result", writeEvents[1].EventStreamId);
            Assert.AreEqual("data1", Encoding.UTF8.GetString(writeEvents[0].Events[0].Data));
            Assert.AreEqual("data2", Encoding.UTF8.GetString(writeEvents[1].Events[0].Data));
        }

    }


    [TestFixture]
    public class when_handling_event_does_not_change_state_the_projection_without_when : specification_with_query_without_when
    {
        protected override void When()
        {
            //projection subscribes here
            _eventId = Guid.NewGuid();
            _consumer.HandledMessages.Clear();
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", 1, "account-01", 1, false, new TFPos(120, 110), _eventId, "handle_this_type",
                        false, "data1", "metadata"), _subscriptionId, 0));
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", 2, "account-01", 2, false, new TFPos(140, 130), _eventId, "handle_this_type",
                        false, "data1", "metadata"), _subscriptionId, 1));
        }

        [Test]
        public void result_events_are_produced_for_each_received_event()
        {
            var writeEvents =
                _writeEventHandler.HandledMessages.Where(v => v.Events.Any(e => e.EventType == "Result")).ToList();
            Assert.AreEqual(2, writeEvents.Count);
            Assert.AreEqual("$projections-projection-result", writeEvents[0].EventStreamId);
            Assert.AreEqual("$projections-projection-result", writeEvents[1].EventStreamId);
            Assert.AreEqual("data1", Encoding.UTF8.GetString(writeEvents[0].Events[0].Data));
            Assert.AreEqual("data1", Encoding.UTF8.GetString(writeEvents[1].Events[0].Data));
        }

        [Test]
        public void no_result_removed_events_are_produced()
        {
            var writeEvents =
                _writeEventHandler.HandledMessages.Where(v => v.Events.Any(e => e.EventType == "ResultRemoved")).ToList();
            Assert.AreEqual(0, writeEvents.Count);
        }

    }

}
