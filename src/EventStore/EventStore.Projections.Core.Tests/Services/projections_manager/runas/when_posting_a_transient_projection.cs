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

using System;
using System.Collections.Generic;
using System.Security.Principal;
using EventStore.Core.Authentication;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.runas
{
    namespace when_posting_a_transient_projection
    {

        [TestFixture]
        public class authenticated : TestFixtureWithProjectionCoreAndManagementServices
        {
            private string _projectionName;
            private OpenGenericPrincipal _testUserPrincipal;

            private string _projectionBody = @"fromAll().whenAny(function(s,e){return s;});";

            protected override void Given()
            {
                _projectionName = "test-projection";
                _projectionBody = @"fromAll().whenAny(function(s,e){return s;});";
                _testUserPrincipal = new OpenGenericPrincipal(
                    new GenericIdentity("test-user"), new[] {"test-role1", "test-role2"});

                AllWritesSucceed();
                NoOtherStreams();
            }

            protected override IEnumerable<WhenStep> When()
            {
                yield return new SystemMessage.BecomeMaster(Guid.NewGuid());
                yield return
                    new ProjectionManagementMessage.Post(
                        new PublishEnvelope(GetInputQueue()), ProjectionMode.Transient, _projectionName,
                        new ProjectionManagementMessage.RunAs(_testUserPrincipal), "JS", _projectionBody, enabled: true,
                        checkpointsEnabled: true, emitEnabled: true, enableRunAs: true);
            }

            [Test, Ignore]
            public void anonymous_cannot_retrieve_projection_query()
            {
                GetInputQueue()
                    .Publish(
                        new ProjectionManagementMessage.GetQuery(
                            Envelope, _projectionName, ProjectionManagementMessage.RunAs.Anonymous));
                _queue.Process();

                Assert.IsTrue(HandledMessages.OfType<ProjectionManagementMessage.NotAuthorized>().Any());
            }

            [Test]
            public void projection_owner_can_retrieve_projection_query()
            {
                GetInputQueue()
                    .Publish(
                        new ProjectionManagementMessage.GetQuery(
                            Envelope, _projectionName, new ProjectionManagementMessage.RunAs(_testUserPrincipal)));
                _queue.Process();

                var query = HandledMessages.OfType<ProjectionManagementMessage.ProjectionQuery>().FirstOrDefault();
                Assert.NotNull(query);
                Assert.AreEqual(_projectionBody, query.Query);
            }

        }

        [TestFixture]
        public class anonymous : TestFixtureWithProjectionCoreAndManagementServices
        {
            private string _projectionName;
            private OpenGenericPrincipal _testUserPrincipal;

            private string _projectionBody = @"fromAll().whenAny(function(s,e){return s;});";

            protected override void Given()
            {
                _projectionName = "test-projection";
                _projectionBody = @"fromAll().whenAny(function(s,e){return s;});";
                _testUserPrincipal = new OpenGenericPrincipal(
                    new GenericIdentity("test-user"), new[] { "test-role1", "test-role2" });

                AllWritesSucceed();
                NoOtherStreams();
            }

            protected override IEnumerable<WhenStep> When()
            {
                yield return new SystemMessage.BecomeMaster(Guid.NewGuid());
                yield return
                    new ProjectionManagementMessage.Post(
                        new PublishEnvelope(GetInputQueue()), ProjectionMode.Continuous, _projectionName,
                        ProjectionManagementMessage.RunAs.Anonymous, "JS", _projectionBody, enabled: true,
                        checkpointsEnabled: true, emitEnabled: true, enableRunAs: true);
            }

            [Test]
            public void replies_with_not_authorized()
            {
                Assert.IsTrue(HandledMessages.OfType<ProjectionManagementMessage.NotAuthorized>().Any());
            }

        }

    }


}
