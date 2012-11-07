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

using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public abstract class TestFixtureWithProjectionCoreAndManagementServices : TestFixtureWithExistingEvents
    {
        protected ProjectionManager _manager;
        private ProjectionCoreService _coreService;

        [SetUp]
        public void setup()
        {
            //TODO: this became a n integration test - proper ProjectionCoreService and ProjectionManager testing is required instead
            _bus.Subscribe(_consumer);

            _manager = new ProjectionManager(_bus, _bus, new IPublisher[] {_bus}, checkpointForStatistics: null);
            _coreService = new ProjectionCoreService(_bus, _bus, 10, new InMemoryCheckpoint(1000));
            _bus.Subscribe<ProjectionMessage.Projections.StatusReport.Started>(_manager);
            _bus.Subscribe<ProjectionMessage.Projections.StatusReport.Stopped>(_manager);
            _bus.Subscribe<ProjectionMessage.Projections.Management.StateReport>(_manager);
            _bus.Subscribe<ProjectionMessage.Projections.Management.StatisticsReport>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);

            _bus.Subscribe<ProjectionMessage.CoreService.Management.Create>(_coreService);
            _bus.Subscribe<ProjectionMessage.CoreService.Management.Dispose>(_coreService);
            _bus.Subscribe<ProjectionMessage.Projections.Management.Start>(_coreService);
            _bus.Subscribe<ProjectionMessage.Projections.Management.Stop>(_coreService);
            _bus.Subscribe<ProjectionMessage.Projections.Management.GetState>(_coreService);
            _bus.Subscribe<ProjectionMessage.Projections.Management.UpdateStatistics>(_coreService);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_coreService);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_coreService);


            When();
        }

        protected abstract void When();
    }
}
