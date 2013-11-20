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
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection
{
    [TestFixture]
    public class when_loading_a_managed_projection_state : TestFixtureWithExistingEvents
    {
        private new ITimeProvider _timeProvider;

        private ManagedProjection _mp;

        protected override void Given()
        {
            _timeProvider = new FakeTimeProvider();
            _mp = new ManagedProjection(
                _bus, Guid.NewGuid(), 1, "name", true, null, _writeDispatcher, _readDispatcher, _bus, _bus, _handlerFactory,
                _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_handler_type_throws_argument_null_exception()
        {
            ProjectionManagementMessage.Post message = new ProjectionManagementMessage.Post(
                new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous,
                (string)null, @"log(1);", enabled: true, checkpointsEnabled: false, emitEnabled: false);
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
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_handler_type_throws_argument_null_exception()
        {
            ProjectionManagementMessage.Post message = new ProjectionManagementMessage.Post(
                new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous, "",
                @"log(1);", enabled: true, checkpointsEnabled: false, emitEnabled: false);
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
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_query_throws_argument_null_exception()
        {
            ProjectionManagementMessage.Post message = new ProjectionManagementMessage.Post(
                new NoopEnvelope(), ProjectionMode.OneTime, "name", ProjectionManagementMessage.RunAs.Anonymous,
                "JS", query: null, enabled: true, checkpointsEnabled: false, emitEnabled: false);
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
        }
    }
}
