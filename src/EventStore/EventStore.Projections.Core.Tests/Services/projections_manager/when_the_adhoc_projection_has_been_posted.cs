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
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    [TestFixture]
    public class when_the_adhoc_projection_has_been_posted: TestFixtureWithProjectionCoreAndManagementServices
    {
        private string _projectionName;
        private string _projectionQuery;

        protected override void When()
        {
            _projectionQuery = @"fromAll(); on_any(function(){});log(1);";
            _manager.Handle(new ProjectionManagementMessage.Post(new PublishEnvelope(_bus), _projectionQuery, enabled: true));
            _projectionName = _consumer.HandledMessages.OfType<ProjectionManagementMessage.Updated>().Single().Name;
        }

        [TearDown]
        public void TearDown()
        {
            _manager.Dispose();
        }

        [Test, Category("v8")]
        public void it_has_been_posted()
        {
            Assert.IsNotNullOrEmpty(_projectionName);
        }

        [Test, Category("v8")]
        public void it_cab_be_listed()
        {
            _manager.Handle(
                new ProjectionManagementMessage.GetStatistics(new PublishEnvelope(_bus), null, null, false));

            Assert.AreEqual(
                1,
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count(
                    v => v.Projections.Any(p => p.Name == _projectionName)));
        }

        [Test, Category("v8")]
        public void the_projection_status_can_be_retrieved()
        {
            _manager.Handle(
                new ProjectionManagementMessage.GetStatistics(
                    new PublishEnvelope(_bus), null, _projectionName, false));

            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Count());
            Assert.AreEqual(
                1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Length);
            Assert.AreEqual(
                _projectionName,
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections.Single().Name);
        }

        [Test, Category("v8")]
        public void the_projection_state_can_be_retrieved()
        {
            _manager.Handle(new ProjectionManagementMessage.GetState(new PublishEnvelope(_bus), _projectionName, ""));

            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Count());
            Assert.AreEqual(
                _projectionName, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().Name);
            Assert.AreEqual(
                "", _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionState>().Single().State);
        }

        [Test, Category("v8")]
        public void the_projection_source_can_be_retrieved()
        {
            _manager.Handle(new ProjectionManagementMessage.GetQuery(new PublishEnvelope(_bus), _projectionName));
            Assert.AreEqual(1, _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Count());
            var projectionQuery = _consumer.HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().Single();
            Assert.AreEqual(_projectionName, projectionQuery.Name);
            Assert.AreEqual(_projectionQuery, projectionQuery.Query);
        }
    }
}
