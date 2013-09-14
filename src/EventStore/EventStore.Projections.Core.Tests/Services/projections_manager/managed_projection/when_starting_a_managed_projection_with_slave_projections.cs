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
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using TestFixtureWithExistingEvents = EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithExistingEvents;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection
{
    [TestFixture]
    public class when_starting_a_managed_projection_with_slave_projections : TestFixtureWithExistingEvents
    {
        private new ITimeProvider _timeProvider;

        private ManagedProjection _mp;
        private Guid _coreProjectionId;
        private string _projectionName;
        private SlaveProjectionDefinitions _slaveProjectionDefinitions;

        [SetUp]
        public new void SetUp()
        {
            WhenLoop();
        }

        protected override ManualQueue GiveInputQueue()
        {
            return new ManualQueue(_bus);
        }

        protected override void Given()
        {
            _projectionName = "projection";
            _coreProjectionId = Guid.NewGuid();
            _slaveProjectionDefinitions =
                new SlaveProjectionDefinitions(
                    new[]
                    {
                        new SlaveProjectionDefinitions.Definition(
                            "slave", StateHandlerFactory(), "",
                            SlaveProjectionDefinitions.SlaveProjectionRequestedNumber.OnePerThread,
                            ProjectionMode.Transient, false, false, true, ProjectionManagementMessage.RunAs.System)
                    });
            _timeProvider = new FakeTimeProvider();
            _mp = new ManagedProjection(
                _bus, Guid.NewGuid(), 1, "name", true, null, _writeDispatcher, _readDispatcher, _bus, _bus,
                _handlerFactory, _timeProvider);
        }

        private static string StateHandlerFactory()
        {
            var handlerType = typeof(FakeFromCatalogStreamProjection);
            return "native:" + handlerType.Namespace + "." + handlerType.Name;
        }

        protected override IEnumerable<WhenStep> When()
        {
            ProjectionManagementMessage.Post message = new ProjectionManagementMessage.Post(
                Envelope, ProjectionMode.Transient, _projectionName, ProjectionManagementMessage.RunAs.System,
                typeof (FakeFromCatalogStreamProjection), "", true, false, false);
            _mp.InitializeNew(() => { }, new ManagedProjection.PersistedState
                {
                    Enabled = message.Enabled,
                    HandlerType = message.HandlerType,
                    Query = message.Query,
                    Mode = message.Mode,
                    EmitEnabled = message.EmitEnabled,
                    CheckpointsDisabled = !message.CheckpointsEnabled,
                    Epoch = -1,
                    Version = -1,
                    RunAs = message.EnableRunAs ? ManagedProjection.SerializePrincipal(message.RunAs) : null,
                });

            var sourceDefinition = new FakeFromCatalogStreamProjection("", Console.WriteLine).GetSourceDefinition();
            var projectionSourceDefinition = ProjectionSourceDefinition.From(
                _projectionName, sourceDefinition, message.HandlerType, message.Query);

            var slaveProjectionDefinitions = _slaveProjectionDefinitions;

            _mp.Handle(
                new CoreProjectionManagementMessage.Prepared(
                    _coreProjectionId, projectionSourceDefinition, slaveProjectionDefinitions));
            yield break;
        }

        [Test]
        public void publishes_start_slave_projections_message()
        {
            var startSlaveProjectionsMessage =
                HandledMessages.OfType<ProjectionManagementMessage.StartSlaveProjections>().LastOrDefault();
            Assert.IsNotNull(startSlaveProjectionsMessage);

        }

        [Test]
        public void publishes_start_message()
        {
            var publisher1 = new FakePublisher();
            var publisher2 = new FakePublisher();

            var channel1 = new SlaveProjectionCommunicationChannel(Guid.Empty, Guid.NewGuid(), publisher1);
            var channel2 = new SlaveProjectionCommunicationChannel(Guid.Empty, Guid.NewGuid(), publisher2);
            var channels =
                new SlaveProjectionCommunicationChannels(
                    new Dictionary<string, SlaveProjectionCommunicationChannel[]>
                    {
                        {"slave", new[] {channel1, channel2}}
                    });
            _mp.Handle(new ProjectionManagementMessage.SlaveProjectionsStarted(_projectionName, channels));
            var startMessage = HandledMessages.OfType<CoreProjectionManagementMessage.Start>().LastOrDefault();
            Assert.IsNotNull(startMessage);
            Assert.IsNotNull(startMessage.SlaveProjections);
            Assert.AreEqual(1, startMessage.SlaveProjections.Channels.Count);
            Assert.AreEqual("slave", startMessage.SlaveProjections.Channels.First().Key);
            Assert.AreEqual(2, startMessage.SlaveProjections.Channels.First().Value.Length);
        }
    }
}
