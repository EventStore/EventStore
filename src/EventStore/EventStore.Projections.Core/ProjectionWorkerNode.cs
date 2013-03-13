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
            coreInputBus.Subscribe<ProjectionCoreServiceMessage.Start>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionCoreServiceMessage.Stop>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionCoreServiceMessage.Tick>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepare>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.CreatePrepared>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.Dispose>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionSubscriptionManagement.Subscribe>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionSubscriptionManagement.Unsubscribe>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionSubscriptionManagement.Pause>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionSubscriptionManagement.Resume>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionCoreServiceMessage.CommittedEventDistributed>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionCoreServiceMessage.EventReaderIdle>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionCoreServiceMessage.EventReaderEof>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionSubscriptionMessage.CommittedEventReceived>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionSubscriptionMessage.CheckpointSuggested>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionSubscriptionMessage.EofReached>(_projectionCoreService);
            coreInputBus.Subscribe<ProjectionSubscriptionMessage.ProgressChanged>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.Start>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.LoadStopped>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.Stop>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.Kill>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.GetState>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.GetResult>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.GetDebugState>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionManagementMessage.UpdateStatistics>(_projectionCoreService);
            coreInputBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_projectionCoreService);
            coreInputBus.Subscribe<ClientMessage.WriteEventsCompleted>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionProcessingMessage.CheckpointCompleted>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionProcessingMessage.CheckpointLoaded>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionProcessingMessage.RestartRequested>(_projectionCoreService);
            coreInputBus.Subscribe<CoreProjectionProcessingMessage.Failed>(_projectionCoreService);
            //NOTE: message forwarding is set up outside (for Read/Write events)
        }
    }
}