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
using EventStore.Core.Bus;
using EventStore.Core.Tests.Fakes;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    [TestFixture]
    public class when_handling_start_slave_projections_message: specification_with_projection_management_service
    {

        protected FakePublisher _coreQueue1;
        protected FakePublisher _coreQueue2;
        private string _masterProjectionName;
        private SlaveProjectionDefinitions _slaveProjectionDefinitions;
        private Guid _masterCorrelationId;

        protected override IPublisher[] GivenCoreQueues()
        {
            _coreQueue1 = new FakePublisher();
            _coreQueue2 = new FakePublisher();
            return new[] { _coreQueue1, _coreQueue2 };
        }

        protected override void Given()
        {
            base.Given();
            _masterProjectionName = "master-projection";
            _masterCorrelationId = Guid.NewGuid();
            _slaveProjectionDefinitions =
                new SlaveProjectionDefinitions(
                    new SlaveProjectionDefinitions.Definition(
                        "slave", StateHandlerFactory(), "",
                        SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerThread, ProjectionMode.Transient,
                        false, false, true, ProjectionManagementMessage.RunAs.System));
        }

        protected override IEnumerable<WhenStep> When()
        {
            foreach (var m in base.When()) yield return m;

            yield return
                new ProjectionManagementMessage.StartSlaveProjections(
                    Envelope, ProjectionManagementMessage.RunAs.System, _masterProjectionName,
                    _slaveProjectionDefinitions, GetInputQueue(), _masterCorrelationId);
        }

        private static string StateHandlerFactory()
        {
            var handlerType = typeof(FakeFromCatalogStreamProjection);
            return "native:" + handlerType.Namespace + "." + handlerType.Name;
        }

        [Test, Ignore("test framework.")]
        public void publishes_slave_projections_started_message()
        {
            var startedMessages = HandledMessages.OfType<ProjectionManagementMessage.SlaveProjectionsStarted>().ToArray();
            Assert.AreEqual(1, startedMessages);
        }

    }
}
