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
using EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.heading_distribution_point
{
    [TestFixture]
    public class when_the_heading_distribution_point_with_a_subscribed_projection_handles_an_event :
        TestFixtureWithReadWriteDisaptchers
    {
        private HeadingEventDistributionPoint _point;
        private Exception _exception;
        private Guid _distibutionPointCorrelationId;
        private FakeProjectionSubscription _subscription;
        private Guid _projectionSubscriptionId;

        [SetUp]
        public void setup()
        {
            _exception = null;
            try
            {
                _point = new HeadingEventDistributionPoint(10);
            }
            catch (Exception ex)
            {
                _exception = ex;
            }

            _distibutionPointCorrelationId = Guid.NewGuid();
            _point.Start(
                _distibutionPointCorrelationId,
                new TransactionFileReaderEventDistributionPoint(
                    _bus, _distibutionPointCorrelationId, new EventPosition(0, -1)));
            _point.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    _distibutionPointCorrelationId, new EventPosition(20, 10), "stream", 10, false,
                    ResolvedEvent.Sample(Guid.NewGuid(), "type", false, new byte[0], new byte[0])));
            _point.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    _distibutionPointCorrelationId, new EventPosition(40, 30), "stream", 11, false,
                    ResolvedEvent.Sample(Guid.NewGuid(), "type", false, new byte[0], new byte[0])));
            _subscription = new FakeProjectionSubscription();
            _projectionSubscriptionId = Guid.NewGuid();
            var subscribed = _point.TrySubscribe(_projectionSubscriptionId, _subscription, 30);
            _point.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    _distibutionPointCorrelationId, new EventPosition(60, 50), "stream", 12, false,
                    ResolvedEvent.Sample(Guid.NewGuid(), "type", false, new byte[0], new byte[0])));
        }


        [Test]
        public void projection_receives_events_after_the_subscription_point()
        {
            Assert.AreEqual(50, _subscription.ReceivedEvents.Last().Position.PreparePosition);
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
