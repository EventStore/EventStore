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
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Http;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core
{
    public class ProjectionWorkerNode : IHandle<ProjectionMessage.CoreService.Start>
    {
        private readonly IPublisher _coreQueue;

        private readonly ProjectionCoreService _projectionCoreService;
        private InMemoryBus _coreOutput;
        private readonly ProjectionManager _projectionManager;

        public ProjectionWorkerNode(TFChunkDb db, IPublisher coreQueue, HttpService httpService)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(coreQueue, "coreQueue");

            _coreQueue = coreQueue;
            CoreOutput = new InMemoryBus("Core Output");

            _projectionCoreService = new ProjectionCoreService(CoreOutput, 10, db.Config.WriterCheckpoint);
            CoreOutput.Subscribe(Forwarder.Create<Message>(_coreQueue)); // forward all

            _projectionManager = new ProjectionManager(CoreOutput, db.Config.WriterCheckpoint);

            httpService.SetupController(new ProjectionsController(_coreQueue));
        }

        public InMemoryBus CoreOutput
        {
            get { return _coreOutput; }
            set { _coreOutput = value; }
        }

        public void SetupMessaging(InMemoryBus coreInputBus, InMemoryBus readerInputBus)
        {
            coreInputBus.Subscribe<ProjectionMessage.CoreService.Start>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.CoreService.Stop>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.CoreService.Tick>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.SubscribeProjection>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.UnsubscribeProjection>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.PauseProjectionSubscription>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.ResumeProjectionSubscription>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.CommittedEventReceived>(_projectionCoreService);

            coreInputBus.Subscribe<SystemMessage.SystemInit>(_projectionManager);
            coreInputBus.Subscribe<SystemMessage.SystemStart>(_projectionManager);
            coreInputBus.Subscribe<ProjectionManagementMessage.Post>(_projectionManager);
            coreInputBus.Subscribe<ProjectionManagementMessage.UpdateQuery>(_projectionManager);
            coreInputBus.Subscribe<ProjectionManagementMessage.GetQuery>(_projectionManager);
            coreInputBus.Subscribe<ProjectionManagementMessage.Delete>(_projectionManager);
            coreInputBus.Subscribe<ProjectionManagementMessage.GetStatistics>(_projectionManager);
            coreInputBus.Subscribe<ProjectionManagementMessage.GetState>(_projectionManager);
            coreInputBus.Subscribe<ProjectionManagementMessage.Disable>(_projectionManager);
            coreInputBus.Subscribe<ProjectionManagementMessage.Enable>(_projectionManager);
            coreInputBus.Subscribe<ProjectionMessage.Projections.Started>(_projectionManager);
            coreInputBus.Subscribe<ProjectionMessage.Projections.Stopped>(_projectionManager);
            coreInputBus.Subscribe<ProjectionMessage.Projections.Faulted>(_projectionManager);
            coreInputBus.Subscribe<ClientMessage.WriteEventsCompleted>(_projectionManager);
            coreInputBus.Subscribe<ClientMessage.ReadEventsBackwardsCompleted>(_projectionManager);
            //NOTE: message forwarding is set up outside (for Read/Write events)


            coreInputBus.Subscribe(this);
        }

        public void Handle(ProjectionMessage.CoreService.Start message)
        {
        }
    }
}