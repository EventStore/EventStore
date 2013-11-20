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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    [TestFixture]
    public class when_posting_a_persistent_projection : TestFixtureWithProjectionCoreAndManagementServices
    {
        private string _projectionName;

        protected override void Given()
        {
            _projectionName = "test-projection";
            AllWritesQueueUp();
        }

        protected override IEnumerable<WhenStep> When()
        {
            yield return new SystemMessage.BecomeMaster(Guid.NewGuid());
            yield return
                new ProjectionManagementMessage.Post(
                    new PublishEnvelope(_bus), ProjectionMode.Continuous, _projectionName,
                    ProjectionManagementMessage.RunAs.System, "JS", @"fromAll().whenAny(function(s,e){return s;});",
                    enabled: true, checkpointsEnabled: true, emitEnabled: true);
            OneWriteCompletes();
            yield return Yield;
        }

        [Test, Category("v8")]
        public void the_projection_status_is_writing()
        {
            _manager.Handle(
                new ProjectionManagementMessage.GetStatistics(new PublishEnvelope(_bus), null, _projectionName, true));
            Assert.AreEqual(
                ManagedProjectionState.Writing,
                _consumer.HandledMessages.OfType<ProjectionManagementMessage.Statistics>().Single().Projections[0].
                    MasterStatus);
        }

        [Test, Category("v8")]
        public void a_projection_created_event_is_written()
        {
            Assert.AreEqual(
                "$ProjectionCreated",
                _consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().First().Events[0].EventType);
            Assert.AreEqual(
                _projectionName,
                Helper.UTF8NoBom.GetString(_consumer.HandledMessages.OfType<ClientMessage.WriteEvents>().First().Events[0].Data));
        }

        [Test, Category("v8")]
        public void a_projection_updated_message_is_not_published()
        {
            // not published until all writes complete
            Assert.AreEqual(0, _consumer.HandledMessages.OfType<ProjectionManagementMessage.Updated>().Count());
        }

    }
}
