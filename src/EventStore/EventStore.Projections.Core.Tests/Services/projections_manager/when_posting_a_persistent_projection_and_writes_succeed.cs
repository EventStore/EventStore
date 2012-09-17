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

using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    [TestFixture]
    public class when_posting_a_persistent_projection_and_writes_succeed : TestFixtureWithExistingEvents
    {
        protected override void Given()
        {
            AllWritesSucceed();
        }

        private ProjectionManager _manager;
        private string _projectionName;

        [SetUp]
        public void setup()
        {
            _manager = new ProjectionManager(_bus, checkpointForStatistics: null);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _projectionName = "test-projection";
            _manager.Handle(
                new ProjectionManagementMessage.Post(
                    new PublishEnvelope(_bus), ProjectionMode.Persistent, _projectionName, "JS",
                    @"fromAll(); on_any(function(){});log(1);", enabled: true));
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
        }

        [Test]
        public void projection_status_is_running()
        {
            _manager.Handle(
                new ProjectionManagementMessage.GetStatistics(new PublishEnvelope(_bus), null, _projectionName, true));
            Assert.AreEqual(
                ManagedProjectionState.Running,
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections[0].
                    MasterStatus);
        }

        [Test]
        public void a_projection_updated_event_is_written()
        {
            Assert.IsTrue(
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().Any(
                    v => v.Events[0].EventType == "ProjectionUpdated"));
        }

        [Test]
        public void a_projection_updated_message_is_published()
        {
            // not published until writes complete
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Updated>().Count());
        }
    }
}
