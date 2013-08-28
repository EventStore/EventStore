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
using EventStore.Common.Utils;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;
using EventStore.Projections.Core.EventReaders.Feeds;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core
{
    public class ProjectionWorkerNode 
    {
        private readonly RunProjections _runProjections;
        private readonly ProjectionCoreService _projectionCoreService;
        private readonly InMemoryBus _coreOutput;
        private readonly EventReaderCoreService _eventReaderCoreService;

        private readonly ReaderSubscriptionDispatcher
            _subscriptionDispatcher;

        private readonly FeedReaderService _feedReaderService;
        private readonly IODispatcher _ioDispatcher;

        public ProjectionWorkerNode(TFChunkDb db, QueuedHandler inputQueue, ITimeProvider timeProvider, RunProjections runProjections)
        {
            _runProjections = runProjections;
            Ensure.NotNull(db, "db");

            _coreOutput = new InMemoryBus("Core Output");

            IPublisher publisher = CoreOutput;
            _subscriptionDispatcher =
                new ReaderSubscriptionDispatcher(publisher, v => v.SubscriptionId, v => v.SubscriptionId);

            _ioDispatcher = new IODispatcher(publisher, new PublishEnvelope(inputQueue));
            _eventReaderCoreService = new EventReaderCoreService(
                publisher, _ioDispatcher, 10, db.Config.WriterCheckpoint, runHeadingReader: runProjections >= RunProjections.System);
            _feedReaderService = new FeedReaderService(_subscriptionDispatcher, timeProvider);
            if (runProjections >= RunProjections.System)
            {
                _projectionCoreService = new ProjectionCoreService(inputQueue, publisher, _subscriptionDispatcher, timeProvider, _ioDispatcher);
            }
        }

        public InMemoryBus CoreOutput
        {
            get { return _coreOutput; }
        }

        public void SetupMessaging(IBus coreInputBus)
        {
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            coreInputBus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());

            coreInputBus.Subscribe(_feedReaderService);

            if (_runProjections >= RunProjections.System)
            {

                coreInputBus.Subscribe<ProjectionCoreServiceMessage.StartCore>(_projectionCoreService);
                coreInputBus.Subscribe<ProjectionCoreServiceMessage.StopCore>(_projectionCoreService);
                coreInputBus.Subscribe<ProjectionCoreServiceMessage.CoreTick>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.CreateAndPrepare>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.CreatePrepared>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.Dispose>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.Start>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.LoadStopped>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.Stop>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.Kill>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.GetState>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.GetResult>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionManagementMessage.UpdateStatistics>(_projectionCoreService);
                coreInputBus.Subscribe<ClientMessage.ReadStreamEventsForwardCompleted>(_ioDispatcher.ForwardReader);
                coreInputBus.Subscribe<ClientMessage.ReadStreamEventsBackwardCompleted>(_ioDispatcher.BackwardReader);
                coreInputBus.Subscribe<ClientMessage.WriteEventsCompleted>(_ioDispatcher.Writer);
                coreInputBus.Subscribe<ClientMessage.DeleteStreamCompleted>(_ioDispatcher.StreamDeleter);
                coreInputBus.Subscribe<IODispatcherDelayedMessage>(_ioDispatcher);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.CheckpointCompleted>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.CheckpointLoaded>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.PrerecordedEventsLoaded>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.RestartRequested>(_projectionCoreService);
                coreInputBus.Subscribe<CoreProjectionProcessingMessage.Failed>(_projectionCoreService);
                //NOTE: message forwarding is set up outside (for Read/Write events)
            }

            coreInputBus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderCoreServiceMessage.StartReader>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderCoreServiceMessage.StopReader>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Subscribe>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Pause>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionManagement.Resume>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.CommittedEventDistributed>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderEof>(_eventReaderCoreService);
            coreInputBus.Subscribe<ReaderSubscriptionMessage.EventReaderNotAuthorized>(_eventReaderCoreService);
            //NOTE: message forwarding is set up outside (for Read/Write events)

        }
    }
}