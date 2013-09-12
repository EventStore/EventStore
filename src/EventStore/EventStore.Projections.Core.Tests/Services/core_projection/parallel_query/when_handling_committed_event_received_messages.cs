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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services.core_projection.parallel_query
{
    [TestFixture]
    class when_handling_committed_event_received_messages : specification_with_parallel_query
    {
        protected override void Given()
        {
            base.Given();
            _eventId = Guid.NewGuid();
        }

        protected override void When()
        {
            var tag0 = CheckpointTag.FromByStreamPosition(0, "catalog", 0, null, -1, 10000);
            var tag1 = CheckpointTag.FromByStreamPosition(0, "catalog", 1, null, -1, 10000);
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "catalog", 0, "catalog", 0, false, new TFPos(120, 110), _eventId, "$@", false, "test-stream", ""),
                    tag0, _subscriptionId, 0));
            _bus.Publish(
                EventReaderSubscriptionMessage.CommittedEventReceived.Sample(
                    new ResolvedEvent(
                        "catalog", 1, "catalog", 1, false, new TFPos(220, 210), Guid.NewGuid(), "$@", false,
                        "test-stream2", ""), tag1, _subscriptionId, 1));
        }

        [Test]
        public void spools_slave_stream_processing()
        {
            var spooled = HandledMessages.OfType<ReaderSubscriptionManagement.SpoolStreamReading>().ToArray();
            Assert.AreEqual(2, spooled.Length);
        }
    }
}
