﻿// Copyright (c) 2012, Event Store LLP
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

namespace EventStore.Projections.Core.Tests.Services.projection_subscription
{
    [TestFixture]
    public class when_handling_multiple_committed_event_passing_the_filter : TestFixtureWithProjectionSubscription
    {
        protected override void Given()
        {
            base.Given();
            _checkpointProcessedEventsThreshold = 2;
        }

        protected override void When()
        {
            _subscription.Handle(
                ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                    Guid.NewGuid(), new TFPos(200, 150), "test-stream", 1, false, Guid.NewGuid(),
                    "bad-event-type", false, new byte[0], new byte[0]));
            _subscription.Handle(
                ReaderSubscriptionMessage.CommittedEventDistributed.Sample(
                    Guid.NewGuid(), new TFPos(300, 250), "test-stream", 2, false, Guid.NewGuid(),
                    "bad-event-type", false, new byte[0], new byte[0]));
        }

        [Test]
        public void events_passed_to_downstream_handler_have_correct_subscription_sequence_numbers()
        {
            Assert.AreEqual(2, _eventHandler.HandledMessages.Count);

            Assert.AreEqual(0, _eventHandler.HandledMessages[0].SubscriptionMessageSequenceNumber);
            Assert.AreEqual(1, _eventHandler.HandledMessages[1].SubscriptionMessageSequenceNumber);
        }

        [Test]
        public void suggests_a_checkpoint()
        {
            Assert.AreEqual(1, _checkpointHandler.HandledMessages.Count);
            Assert.AreEqual(CheckpointTag.FromPosition(0, 300, 250), _checkpointHandler.HandledMessages[0].CheckpointTag);
        }
    }
}
