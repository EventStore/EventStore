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
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Helper;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reader.heading_event_reader
{
    [TestFixture]
    public class when_the_heading_event_reader_subscribes_a_projection : TestFixtureWithReadWriteDispatchers
    {
        private HeadingEventReader _point;
        private Exception _exception;
        private Guid _distibutionPointCorrelationId;
        private FakeReaderSubscription _subscription;
        private Guid _projectionSubscriptionId;

        [SetUp]
        public void setup()
        {
            _exception = null;
            try
            {
                _point = new HeadingEventReader(10);
            }
            catch (Exception ex)
            {
                _exception = ex;
            }

            _distibutionPointCorrelationId = Guid.NewGuid();
            _point.Start(
                _distibutionPointCorrelationId,
                new TransactionFileEventReader(
                    _bus, _distibutionPointCorrelationId, new TFPos(0, -1), new RealTimeProvider()));
            _point.Handle(
                ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                    _distibutionPointCorrelationId, new TFPos(20, 10), "stream", 10, false, Guid.NewGuid(),
                    "type", false, new byte[0], new byte[0]));
            _point.Handle(
                ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                    _distibutionPointCorrelationId, new TFPos(40, 30), "stream", 11, false, Guid.NewGuid(),
                    "type", false, new byte[0], new byte[0]));
            _subscription = new FakeReaderSubscription();
            _projectionSubscriptionId = Guid.NewGuid();
            var subscribed = _point.TrySubscribe(_projectionSubscriptionId, _subscription, 30);
        }


        [Test]
        public void projection_receives_at_least_one_cached_event_before_the_subscription_position()
        {
            Assert.AreEqual(true, _subscription.ReceivedEvents.Any(v => v.Data.Position.PreparePosition <= 30));
        }

        [Test]
        public void projection_receives_all_the_previously_handled_events_after_the_subscription_position()
        {
            Assert.AreEqual(true, _subscription.ReceivedEvents.Any(v => v.Data.Position.PreparePosition == 30));
        }

        [Test]
        public void it_can_be_unsubscribed()
        {
            _point.Unsubscribe(_projectionSubscriptionId);
        }

        [Test, ExpectedException(typeof (InvalidOperationException))]
        public void no_other_projection_can_subscribe_with_the_same_projection_id()
        {
            var subscribed = _point.TrySubscribe(_projectionSubscriptionId, _subscription, 30);
        }
    }
}
