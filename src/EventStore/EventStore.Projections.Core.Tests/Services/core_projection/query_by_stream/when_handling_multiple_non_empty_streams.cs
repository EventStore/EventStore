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
using EventStore.Core.Data;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection.query_by_stream
{
    [TestFixture]
    [Ignore("This isn't implemented yet")]
    public class when_handling_multiple_non_empty_streams : specification_with_from_catalog_query
    {
        protected override void When()
        {
            //projection subscribes here
            _eventId = Guid.NewGuid();
            _consumer.HandledMessages.Clear();

            var tag0 = CheckpointTag.FromByStreamPosition(0, "catalog", 0, "account-00", 0, long.MinValue);
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-00", 0, "account-00", 0, false, new TFPos(120, 110), _eventId,
                        "handle_this_type", false, "data", "metadata"), tag0, _subscriptionId, 0));
            _bus.Publish(
                new EventReaderSubscriptionMessage.PartitionEofReached(
                    _subscriptionId, tag0,
                    "account-00", 1));


            var tag1 = CheckpointTag.FromByStreamPosition(0, "catalog", 1, "account-01", 0, long.MinValue);
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "account-01", 0, "account-01", 0, false, new TFPos(220, 210), _eventId,
                        "handle_this_type", false, "data", "metadata"), tag1, _subscriptionId, 2));
            _bus.Publish(
                new EventReaderSubscriptionMessage.PartitionEofReached(
                    _subscriptionId, tag1,
                    "account-01", 3));
        }

        [Test]
        public void writes_empty_state_for_each_partition()
        {
            Assert.AreEqual(2, _writeEventHandler.HandledMessages.OfEventType("Result").Count);
            var message = _writeEventHandler.HandledMessages.WithEventType("Result")[0];
            Assert.AreEqual("$projections-projection-account-00-result", message.EventStreamId);
            var message2 = _writeEventHandler.HandledMessages.WithEventType("Result")[1];
            Assert.AreEqual("$projections-projection-account-01-result", message2.EventStreamId);
        }
    }
}
