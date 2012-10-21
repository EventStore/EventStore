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
using EventStore.Common.Utils;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core
{
    public class ProjectionWorkerNode 
    {
        private readonly ProjectionCoreService _projectionCoreService;
        private readonly InMemoryBus _coreOutput;

        public ProjectionWorkerNode(TFChunkDb db, QueuedHandler inputQueue)
        {
            Ensure.NotNull(db, "db");

            _coreOutput = new InMemoryBus("Core Output");

            _projectionCoreService = new ProjectionCoreService(CoreOutput, inputQueue, 10, db.Config.WriterCheckpoint);

        }

        public InMemoryBus CoreOutput
        {
            get { return _coreOutput; }
        }

        public void SetupMessaging(IBus coreInputBus)
        {
            coreInputBus.Subscribe<ProjectionMessage.CoreService.Start>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.CoreService.Stop>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.CoreService.Tick>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.CoreService.Management.Create>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.CoreService.Management.Dispose>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.SubscribeProjection>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.UnsubscribeProjection>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.PauseProjectionSubscription>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.ResumeProjectionSubscription>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.CommittedEventDistributed>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.Management.Start>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.Management.Stop>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.Management.GetState>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionMessage.Projections.Management.UpdateStatistics>(_projectionCoreService);
            coreInputBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_projectionCoreService);
            coreInputBus.Subscribe<ClientMessage.WriteEventsCompleted>(_projectionCoreService);
            //NOTE: message forwarding is set up outside (for Read/Write events)
        }
    }
}