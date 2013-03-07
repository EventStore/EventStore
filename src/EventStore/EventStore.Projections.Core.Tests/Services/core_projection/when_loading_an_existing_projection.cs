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
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_loading_an_existing_projection : TestFixtureWithCoreProjectionLoaded
    {
        private string _testProjectionState = @"{""test"":1}";

        protected override void Given()
        {
            ExistingEvent(
                "$projections-projection-result", "Result",
                @"{""commitPosition"": 100, ""preparePosition"": 50}", _testProjectionState);
            ExistingEvent(
                "$projections-projection-checkpoint", "ProjectionCheckpoint",
                @"{""commitPosition"": 100, ""preparePosition"": 50}", _testProjectionState);
            ExistingEvent(
                "$projections-projection-result", "Result",
                @"{""commitPosition"": 200, ""preparePosition"": 150}", _testProjectionState);
            ExistingEvent(
                "$projections-projection-result", "Result",
                @"{""commitPosition"": 300, ""preparePosition"": 250}", _testProjectionState);
        }

        protected override void When()
        {
        }


        [Test]
        public void should_subscribe_from_the_last_known_checkpoint_position()
        {
            Assert.AreEqual(1, _subscribeProjectionHandler.HandledMessages.Count);
            Assert.AreEqual(100, _subscribeProjectionHandler.HandledMessages[0].FromPosition.Position.CommitPosition);
            Assert.AreEqual(50, _subscribeProjectionHandler.HandledMessages[0].FromPosition.Position.PreparePosition);
        }

        [Test]
        public void should_subscribe_non_null_subscriber()
        {
            Assert.NotNull(_subscribeProjectionHandler.HandledMessages[0].Subscriber);
        }

        [Test]
        public void should_not_load_projection_state_handler()
        {
            Assert.AreEqual(0, _stateHandler._loadCalled);
        }

        [Test]
        public void should_not_publish_started_message()
        {
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<CoreProjectionManagementMessage.Started>().Count());
        }
    }
}
