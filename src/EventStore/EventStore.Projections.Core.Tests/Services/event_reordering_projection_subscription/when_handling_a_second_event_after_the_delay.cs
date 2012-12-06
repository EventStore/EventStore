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
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reordering_projection_subscription
{
    [TestFixture]
    public class when_handling_a_second_event_after_the_delay : TestFixtureWithEventReorderingProjectionSubscription
    {
        private Guid _firstEventId;
        private DateTime _firstEventTimestamp;
        private int _timeBetweenEvents;

        protected override void When()
        {
            _firstEventId = Guid.NewGuid();
            _firstEventTimestamp = DateTime.UtcNow;
            _timeBetweenEvents = 1100;

            _subscription.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    Guid.NewGuid(), new EventPosition(200, 150), "a", 1, false,
                    ResolvedEvent.Sample(
                        _firstEventId, "bad-event-type", false, new byte[0], new byte[0], _firstEventTimestamp)));
            _subscription.Handle(
                new ProjectionCoreServiceMessage.CommittedEventDistributed(
                    Guid.NewGuid(), new EventPosition(300, 250), "a", 2, false,
                    ResolvedEvent.Sample(
                        Guid.NewGuid(), "bad-event-type", false, new byte[0], new byte[0],
                        _firstEventTimestamp.AddMilliseconds(_timeBetweenEvents))));
        }

        [Test]
        public void no_events_are_passed_to_downstream_handler_immediately()
        {
            Assert.AreEqual(1, _eventHandler.HandledMessages.Count);
        }
    }
}
