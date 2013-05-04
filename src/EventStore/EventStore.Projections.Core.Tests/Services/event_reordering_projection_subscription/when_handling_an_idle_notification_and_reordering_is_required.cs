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
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.event_reordering_projection_subscription
{
    [TestFixture]
    public class when_handling_an_idle_notification_and_reordering_is_required :
        TestFixtureWithEventReorderingProjectionSubscription
    {
        private Guid _firstEventId;
        private DateTime _firstEventTimestamp;
        private Guid _secondEventId;

        protected override void When()
        {
            _firstEventId = Guid.NewGuid();
            _secondEventId = Guid.NewGuid();
            _firstEventTimestamp = DateTime.UtcNow;

            _subscription.Handle(
                ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                    Guid.NewGuid(), new TFPos(200, 150), "a", 1, false, _secondEventId, "bad-event-type", false,
                    new byte[0], new byte[0], _firstEventTimestamp));
            _subscription.Handle(
                ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                    Guid.NewGuid(), new TFPos(300, 100), "b", 1, false, _firstEventId, "bad-event-type", false,
                    new byte[0], new byte[0], _firstEventTimestamp.AddMilliseconds(1)));
            _subscription.Handle(
                new ReaderSubscriptionMessage.EventReaderIdle(
                    Guid.NewGuid(), _firstEventTimestamp.AddMilliseconds(_timeBetweenEvents)));
        }

        [Test]
        public void events_are_reordered()
        {
            Assert.AreEqual(2, _eventHandler.HandledMessages.Count);
            var first = _eventHandler.HandledMessages[0];
            var second = _eventHandler.HandledMessages[1];
            Assert.AreEqual(_firstEventId, first.Data.EventId);
            Assert.AreEqual(_secondEventId, second.Data.EventId);
        }
    }
}
