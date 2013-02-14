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
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_the_checkpoint_is_suggested : TestFixtureWithCoreProjectionStarted
    {
        protected override void Given()
        {
            _checkpointHandledThreshold = 10;
            _checkpointUnhandledBytesThreshold = 41;
            _configureBuilderByQuerySource = source =>
                {
                    source.FromAll();
                    source.IncludeEvent("non-existing");
                };
            NoStream("$projections-projection-result");
            NoStream("$projections-projection-order");
            AllWritesToSucceed("$projections-projection-order");
            NoStream("$projections-projection-checkpoint");
            NoStream(FakeProjectionStateHandler._emit1StreamId);
            AllWritesSucceed();
        }

        protected override void When()
        {
            //projection subscribes here
            _coreProjection.Handle(
                new ProjectionSubscriptionMessage.CheckpointSuggested(
                    Guid.Empty, _subscriptionId, CheckpointTag.FromPosition(140, 130), 55.5f, 0));
        }

        [Test]
        public void a_projection_checkpoint_event_is_published()
        {
            // projection checkpoint is written even though no events are passing the projection event filter
            Assert.AreEqual(
                1,
                _writeEventHandler.HandledMessages.Count(v => v.Events.Any(e => e.EventType == "ProjectionCheckpoint")));
        }
    }
}
