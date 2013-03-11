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
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.continuous
{
    public class a_running_projection
    {
        public abstract class Base : a_new_posted_projection.Base
        {
            protected Guid _reader;

            protected override void Given()
            {
                base.Given();
            }

            protected override void When()
            {
                base.When();
                var readerAssignedMessage =
                    _consumer.HandledMessages.OfType<ProjectionSubscriptionManagement.ReaderAssigned>().LastOrDefault();
                Assert.IsNotNull(readerAssignedMessage);
                _reader = readerAssignedMessage.ReaderId;

                _bus.Publish(
                    ProjectionCoreServiceMessage.CommittedEventDistributed.Sample(
                        _reader, new EventPosition(100, 50), "stream", 1, "stream", 1, false, Guid.NewGuid(), "type",
                        false, new byte[0], new byte[0], 100, 33.3f));
            }
        }

        [TestFixture]
        public class when_stopping : Base
        {
            protected override void When()
            {
                base.When();
                _manager.Handle(new ProjectionManagementMessage.Disable(new PublishEnvelope(_bus), _projectionName));
                for (var i = 0; i < 50; i++)
                {
                    _bus.Publish(
                        ProjectionCoreServiceMessage.CommittedEventDistributed.Sample(
                            _reader, new EventPosition(100*i + 200, 150), "stream", 1, "stream", 1 + i + 1, false,
                            Guid.NewGuid(), "type", false, new byte[0], new byte[0], 100*i + 200, 33.3f));
                }
            }

            [Test]
            public void pause_message_is_published()
            {
                Assert.Inconclusive("actually in unsubscribes...");
            }

            [Test]
            public void unsubscribe_message_is_published()
            {
                Assert.Inconclusive("actually in unsubscribes...");
            }


            [Test]
            public void the_projection_status_becomes_stopped_disabled()
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
                    ManagedProjectionState.Stopped,
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
        public class when_handling_event : Base
        {
            protected override void When()
            {
                base.When();
                _bus.Publish(
                    ProjectionCoreServiceMessage.CommittedEventDistributed.Sample(
                        _reader, new EventPosition(200, 150), "stream", 2, "stream", 1, false, Guid.NewGuid(), "type",
                        false, new byte[0], new byte[0], 100, 33.3f));
            }

            [Test]
            public void the_projection_status_remains_running_enabled()
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

        [TestFixture]
        public class when_resetting : Base
        {
            protected override void Given()
            {
                base.Given();
                _projectionEnabled = false;
            }

            protected override void When()
            {
                base.When();
                _manager.Handle(new ProjectionManagementMessage.Reset(new PublishEnvelope(_bus), _projectionName));
            }

            [Test]
            public void the_projection_epoch_changes()
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
                    2,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Epoch);
                Assert.AreEqual(
                    2,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Version);
            }

            [Test]
            public void the_projection_status_is_enabled_running()
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
                    ManagedProjectionState.Stopped,
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
        public class when_resetting_and_starting : Base
        {
            protected override void Given()
            {
                base.Given();
                _projectionEnabled = false;
            }

            protected override void When()
            {
                base.When();
                _manager.Handle(new ProjectionManagementMessage.Reset(new PublishEnvelope(_bus), _projectionName));
                _manager.Handle(new ProjectionManagementMessage.Enable(new PublishEnvelope(_bus), _projectionName));
                _bus.Publish(
                    ProjectionCoreServiceMessage.CommittedEventDistributed.Sample(
                        _reader, new EventPosition(100, 150), "stream", 1, "stream", 1 + 1, false,
                        Guid.NewGuid(), "type", false, new byte[0], new byte[0], 200, 33.3f));
            }

            [Test]
            public void the_projection_epoch_changes()
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
                    2,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Epoch);
                Assert.AreEqual(
                    3,
                    _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>()
                             .Single()
                             .Projections.Single()
                             .Version);
            }

            [Test]
            public void the_projection_status_is_enabled_running()
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
