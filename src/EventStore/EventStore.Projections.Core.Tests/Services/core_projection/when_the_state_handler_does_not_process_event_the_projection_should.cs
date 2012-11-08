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

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_the_state_handler_does_not_process_event_the_projection_should : TestFixtureWithCoreProjection
    {
        protected override void Given()
        {
            ExistingEvent(
                "$projections-projection-state", "StateUpdated",
                @"{""CommitPosition"": 100, ""PreparePosition"": 50, ""LastSeenEvent"": """
                + Guid.NewGuid().ToString("D") + @"""}", "{}");
            ExistingEvent(
                "$projections-projection-checkpoint", "ProjectionCheckpoint",
                @"{""CommitPosition"": 100, ""PreparePosition"": 50, ""LastSeenEvent"": """
                + Guid.NewGuid().ToString("D") + @"""}", "{}");
        }

        protected override void When()
        {
            //projection subscribes here
            _coreProjection.Handle(
                ProjectionSubscriptionMessage.CommittedEventReceived.Sample(Guid.Empty, new EventPosition(120, 110), "/event_category/1", -1, false,
                       new Event(Guid.NewGuid(), "skip_this_type", false, new byte[0], new byte[0]), 0));
        }

        [Test]
        public void not_update_state_snapshot()
        {
            Assert.AreEqual(0, _writeEventHandler.HandledMessages.Count);
        }
    }
}
