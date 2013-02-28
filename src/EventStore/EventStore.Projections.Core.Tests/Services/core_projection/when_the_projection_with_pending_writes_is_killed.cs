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
    [TestFixture]
    public class when_the_projection_with_pending_writes_is_killed : TestFixtureWithCoreProjectionStarted
    {
        protected override void Given()
        {
            _checkpointHandledThreshold = 2;
            NoStream("$projections-projection-result");
            NoStream("$projections-projection-order");
            AllWritesToSucceed("$projections-projection-order");
            NoStream("$projections-projection-checkpoint");
            NoStream(FakeProjectionStateHandler._emit1StreamId);
            AllWritesQueueUp();
        }

        protected override void When()
        {
            //projection subscribes here
            // just_emit - ensures that each handled event emits a single event
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    Guid.Empty, _subscriptionId, new EventPosition(120, 110), "/event_category/1", -1, false,
                    ResolvedEvent.Sample(
                        Guid.NewGuid(), "just_emit", false, Encoding.UTF8.GetBytes("data1"),
                        Encoding.UTF8.GetBytes("metadata")), 0));
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    Guid.Empty, _subscriptionId, new EventPosition(140, 130), "/event_category/1", -1, false,
                    ResolvedEvent.Sample(
                        Guid.NewGuid(), "just_emit", false, Encoding.UTF8.GetBytes("data2"),
                        Encoding.UTF8.GetBytes("metadata")), 1));
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(
                    Guid.Empty, _subscriptionId, new EventPosition(160, 150), "/event_category/1", -1, false,
                    ResolvedEvent.Sample(
                        Guid.NewGuid(), "just_emit", false, Encoding.UTF8.GetBytes("data3"),
                        Encoding.UTF8.GetBytes("metadata")), 2));
            _coreProjection.Kill();
        }

        [Test]
        public void a_projection_checkpoint_event_is_not_published()
        {
            AllWriteComplete();
            Assert.AreEqual(
                0,
                _writeEventHandler.HandledMessages.Count(v => v.Events.Any(e => e.EventType == "ProjectionCheckpoint")));
        }

        [Test]
        public void other_events_are_not_written_after_the_checkpoint_write()
        {
            AllWriteComplete();
            Assert.AreEqual(2, _writeEventHandler.HandledMessages.Count());
        }
    }
}
