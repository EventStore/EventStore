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

using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    public abstract class specification_with_projection_management_service : TestFixtureWithExistingEvents
    {
        protected ProjectionManager _manager;
        private bool _initializeSystemProjections;


        protected override void Given1()
        {
            base.Given1();
            _initializeSystemProjections = GivenInitializeSystemProjections();
            if (!_initializeSystemProjections)
            {
                ExistingEvent("$projections-$all", "$ProjectionsInitialized", "", "");
            }
        }

        protected virtual bool GivenInitializeSystemProjections()
        {
            return false;
        }

        protected override ManualQueue GiveInputQueue()
        {
            return new ManualQueue(_bus);
        }

        [SetUp]
        public void Setup()
        {
            //TODO: this became an integration test - proper ProjectionCoreService and ProjectionManager testing is required as well
            _bus.Subscribe(_consumer);

            _manager = new ProjectionManager(
                GetInputQueue(), GetInputQueue(), GivenCoreQueues(), _timeProvider, RunProjections.All,
                _initializeSystemProjections);

            IPublisher inputQueue = GetInputQueue();
            IPublisher publisher = GetInputQueue();
            var ioDispatcher = new IODispatcher(publisher, new PublishEnvelope(inputQueue));
            _bus.Subscribe<ProjectionManagementMessage.Internal.CleanupExpired>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Internal.Deleted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Started>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Stopped>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Prepared>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.Faulted>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StateReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.ResultReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.StatisticsReport>(_manager);
            _bus.Subscribe<CoreProjectionManagementMessage.SlaveProjectionReaderAssigned>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.SlaveProjectionsStarted>(_manager);
            
            _bus.Subscribe<ProjectionManagementMessage.Post>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.UpdateQuery>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetQuery>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Delete>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetStatistics>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetState>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.GetResult>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Disable>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Enable>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.SetRunAs>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.Reset>(_manager);
            _bus.Subscribe<ProjectionManagementMessage.StartSlaveProjections>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_manager);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(_manager);
            _bus.Subscribe<SystemMessage.StateChangeMessage>(_manager);

            _bus.Subscribe<ClientMessage.ReadStreamEventsForwardCompleted>(ioDispatcher.ForwardReader);
            _bus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(ioDispatcher.BackwardReader);
            _bus.Subscribe<ClientMessage.WriteEventsCompleted>(ioDispatcher.Writer);
            _bus.Subscribe<ClientMessage.DeleteStreamCompleted>(ioDispatcher.StreamDeleter);
            _bus.Subscribe<IODispatcherDelayedMessage>(ioDispatcher);

            Given();
            WhenLoop();
        }

        protected abstract IPublisher[] GivenCoreQueues();
    }
}
