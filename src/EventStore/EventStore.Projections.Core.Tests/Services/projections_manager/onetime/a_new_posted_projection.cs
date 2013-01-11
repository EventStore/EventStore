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
using EventStore.Core.Cluster;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.onetime
{
    public static class a_new_posted_projection 
    {
        public abstract class Base : TestFixtureWithProjectionCoreAndManagementServices
        {
            protected string _projectionName;

            protected override void Given()
            {
                base.Given();
                _manager.Handle(new SystemMessage.BecomeWorking());

                _projectionName = "test-projection";
                _manager.Handle(
                    new ProjectionManagementMessage.Post(
                        new PublishEnvelope(_bus), ProjectionMode.OneTime, _projectionName,
                        "native:" + typeof(FakeProjection).AssemblyQualifiedName, @"", enabled: true,
                        checkpointsEnabled: false, emitEnabled: false));
            }
        }

        [TestFixture]
        public class when_get_query: Base
        {
            protected override void When()
            {
                _manager.Handle(new ProjectionManagementMessage.GetQuery(new PublishEnvelope(_bus), _projectionName));
            }

            [Test]
            public void returns_correct_source()
            {
                Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Count());
                var projectionQuery =
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Single();
                Assert.AreEqual(_projectionName, projectionQuery.Name);
                Assert.AreEqual("", projectionQuery.Query);
            }
        }

        [TestFixture]
        public class when_get_state : Base
        {
            protected override void When()
            {
                _manager.Handle(new ProjectionManagementMessage.GetState(new PublishEnvelope(_bus), _projectionName, ""));
            }

            [Test]
            public void returns_correct_state()
            {
                Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());
                Assert.AreEqual(
                    _projectionName,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().Name);
                Assert.AreEqual(
                    "", _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().State);
            }
        }

        [TestFixture]
        public class when_failing : Base
        {
            protected override void When()
            {
                var readerAssignedMessage =
                    _consumer.HandledMessages.OfType<ProjectionSubscriptionManagement.ReaderAssigned>().LastOrDefault();
                Assert.IsNotNull(readerAssignedMessage);
                var reader = readerAssignedMessage.ReaderId;

                _bus.Publish(new ProjectionCoreServiceMessage.CommittedEventDistributed(reader, 
                    new EventPosition(100, 50), "stream", 1, "stream", 1, false, ResolvedEvent.Sample(Guid.NewGuid(), "fail", false, new byte[0], new byte[0]), 100, 33.3f));
            }

            [Test]
            public void publishes_faulted_message()
            {
                Assert.AreEqual(1, _consumer.HandledMessages.OfType<CoreProjectionManagementMessage.Faulted>().Count());
            }

            [Test]
            public void the_projection_status_becomes_faulted()
            {
                _manager.Handle(
                    new ProjectionManagementMessage.GetStatistics(new PublishEnvelope(_bus), null, _projectionName, false));

                Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
                Assert.AreEqual(
                    1,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Length);
                Assert.AreEqual(
                    _projectionName,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Single()
                        .Name);
                Assert.AreEqual(
                    ManagedProjectionState.Faulted,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Single()
                        .MasterStatus);
            }

        }

    }
}
