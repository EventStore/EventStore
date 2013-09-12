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
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection.slave_projection
{
    public abstract class specification_with_slave_core_projection : TestFixtureWithCoreProjectionStarted
    {
        protected Guid _eventId;

        protected override bool GivenIsSlaveProjection()
        {
            return true;
        }

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
            return new SlaveQueryProcessingStrategy(
                _projectionName, _version, _stateHandler, _projectionConfig, _stateHandler.GetSourceDefinition(), null,
                GetInputQueue(), _projectionCorrelationId);
        }

        protected override void Given()
        {
            _eventId = Guid.NewGuid();
            _checkpointHandledThreshold = 0;
            _checkpointUnhandledBytesThreshold = 0;

            _configureBuilderByQuerySource = source =>
            {
                source.FromCatalogStream("catalog");
                source.AllEvents();
                source.SetOutputState();
                source.SetByStream();
            };
            TicksAreHandledImmediately();
            AllWritesSucceed();
            NoOtherStreams();
        }

        protected override FakeProjectionStateHandler GivenProjectionStateHandler()
        {
            return new FakeProjectionStateHandler(
                configureBuilder: _configureBuilderByQuerySource, failOnGetPartition: false);
        }
    }

    [TestFixture]
    class when_processes_one_partition : specification_with_slave_core_projection
    {
        protected override void When()
        {
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", -1, "account-01", 0, false, new TFPos(120, 110), Guid.NewGuid(),
                        "handle_this_type", false, "data", "metadata"),
                    CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", 0, 500), _subscriptionId, 0));
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", -1, "account-01", 1, false, new TFPos(220, 210), _eventId, "handle_this_type",
                        false, "data", "metadata"), CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", 1, 500),
                    _subscriptionId, 1));
            _bus.Publish(
                new EventReaderSubscriptionMessage.PartitionEofReached(
                    _subscriptionId, CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", int.MaxValue, 500),
                    "account-01", 2));
        }

        [Test]
        public void publishes_partition_processing_result_message()
        {
            var results = HandledMessages.OfType<PartitionProcessingResult>().ToArray();
            Assert.AreEqual(1, results.Length);
        }
    }

    [TestFixture]
    class when_processes_multiple_partitions : specification_with_slave_core_projection
    {
        protected override void When()
        {
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", -1, "account-01", 0, false, new TFPos(120, 110), Guid.NewGuid(),
                        "handle_this_type", false, "data", "metadata"),
                    CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", 0, 500), _subscriptionId, 0));
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", -1, "account-01", 1, false, new TFPos(220, 210), _eventId, "handle_this_type",
                        false, "data", "metadata"), CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", 1, 500),
                    _subscriptionId, 1));
            _bus.Publish(
                new EventReaderSubscriptionMessage.PartitionEofReached(
                    _subscriptionId, CheckpointTag.FromByStreamPosition(0, "", 0, "account-01", int.MaxValue, 500),
                    "account-01", 2));
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-02", -1, "account-02", 0, false, new TFPos(220, 210), _eventId, "handle_this_type",
                        false, "data", "metadata"), CheckpointTag.FromByStreamPosition(0, "", 1, "account-02", 0, 500),
                    _subscriptionId, 3));
            _bus.Publish(
                new EventReaderSubscriptionMessage.PartitionEofReached(
                    _subscriptionId, CheckpointTag.FromByStreamPosition(0, "", 1, "account-02", int.MaxValue, 500),
                    "account-01", 4));
        }

        [Test]
        public void publishes_all_partition_processing_result_messages()
        {
            var results = HandledMessages.OfType<PartitionProcessingResult>().ToArray();
            Assert.AreEqual(2, results.Length);
        }
    }
}
