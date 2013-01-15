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
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.onetime
{
    public class a_failed_projection
    {
        public abstract class Base : a_new_posted_projection.Base
        {
            protected override void Given()
            {
                base.Given();
                _projectionSource = "fail";
            }

            protected override void When()
            {
                base.When();
                var readerAssignedMessage =
                    _consumer.HandledMessages.OfType<ProjectionSubscriptionManagement.ReaderAssigned>().LastOrDefault();
                Assert.IsNotNull(readerAssignedMessage);
                var reader = readerAssignedMessage.ReaderId;
                _bus.Publish(
                    new ProjectionCoreServiceMessage.CommittedEventDistributed(
                        reader, new EventPosition(100, 50), "stream", 1, "stream", 1, false,
                        ResolvedEvent.Sample(Guid.NewGuid(), "event", false, new byte[0], new byte[0]), 100, 33.3f));
            }
        }

        [TestFixture]
        public class when_updating_query : Base
        {
            protected override void When()
            {
                base.When();
                _manager.Handle(
                    new ProjectionManagementMessage.UpdateQuery(
                        new PublishEnvelope(_bus), _projectionName,
                        "native:" + typeof (FakeProjection).AssemblyQualifiedName, @"", null));
            }

            [Test]
            public void the_projection_status_becomes_running()
            {
                _manager.Handle(
                    new ProjectionManagementMessage.GetStatistics(
                        new PublishEnvelope(_bus), null, _projectionName, false));

                Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
                Assert.AreEqual(
                    1,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Length);
                Assert.AreEqual(
                    _projectionName,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Name);
                Assert.AreEqual(
                    ManagedProjectionState.Running,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .MasterStatus);
            }
        }

        [TestFixture]
        public class when_stopping : Base
        {
            protected override void When()
            {
                base.When();
                _manager.Handle(new ProjectionManagementMessage.Disable(new PublishEnvelope(_bus), _projectionName));
            }

            [Test]
            public void the_projection_status_becomes_faulted_disabled()
            {
                _manager.Handle(
                    new ProjectionManagementMessage.GetStatistics(
                        new PublishEnvelope(_bus), null, _projectionName, false));

                Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
                Assert.AreEqual(
                    1,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Length);
                Assert.AreEqual(
                    _projectionName,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Name);
                Assert.AreEqual(
                    ManagedProjectionState.Faulted,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .MasterStatus);
                Assert.AreEqual(
                    false,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Enabled);
            }
        }

        [TestFixture]
        public class when_starting : Base
        {
            protected override void When()
            {
                base.When();
                _manager.Handle(new ProjectionManagementMessage.Enable(new PublishEnvelope(_bus), _projectionName));
            }

            [Test]
            public void the_projection_status_becomes_running_enabled() // as we restart
            {
                _manager.Handle(
                    new ProjectionManagementMessage.GetStatistics(
                        new PublishEnvelope(_bus), null, _projectionName, false));

                Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
                Assert.AreEqual(
                    1,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Length);
                Assert.AreEqual(
                    _projectionName,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Name);
                Assert.AreEqual(
                    ManagedProjectionState.Running,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .MasterStatus);
                Assert.AreEqual(
                    true,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Enabled);
            }
        }


    }
}
