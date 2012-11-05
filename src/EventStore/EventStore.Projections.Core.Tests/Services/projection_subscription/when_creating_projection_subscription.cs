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
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription
{
    [TestFixture]
    public class when_creating_projection_subscription
    {
        [Test]
        public void it_can_be_created()
        {
            var ps = new ProjectionSubscription(
                Guid.NewGuid(), CheckpointTag.FromPosition(0, -1),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.CommittedEventReceived>(),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.CheckpointSuggested>(),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.ProgressChanged>(), CreateCheckpointStrategy(),
                1000);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_event_handler_throws_argument_null_exception()
        {
            var ps = new ProjectionSubscription(
                Guid.NewGuid(), CheckpointTag.FromPosition(0, -1), null,
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.CheckpointSuggested>(),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.ProgressChanged>(), CreateCheckpointStrategy(),
                1000);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_ceckpoint_handler_throws_argument_null_exception()
        {
            var ps = new ProjectionSubscription(
                Guid.NewGuid(), CheckpointTag.FromPosition(0, -1),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.CommittedEventReceived>(), null,
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.ProgressChanged>(), CreateCheckpointStrategy(),
                1000);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_progress_handler_throws_argument_null_exception()
        {
            var ps = new ProjectionSubscription(
                Guid.NewGuid(), CheckpointTag.FromPosition(0, -1),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.CommittedEventReceived>(),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.CheckpointSuggested>(), null,
                CreateCheckpointStrategy(), 1000);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_describe_source_throws_argument_null_exception()
        {
            var ps = new ProjectionSubscription(
                Guid.NewGuid(), CheckpointTag.FromPosition(0, -1),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.CommittedEventReceived>(),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.CheckpointSuggested>(),
                new TestMessageHandler<ProjectionMessage.SubscriptionMessage.ProgressChanged>(), null, 1000);
        }

        private CheckpointStrategy CreateCheckpointStrategy()
        {
            var result = new CheckpointStrategy.Builder();
            result.FromAll();
            result.AllEvents();
            return result.Build(ProjectionMode.Persistent);
        }
    }
}
