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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services.core_projection.multi_phase
{
    abstract class specification_with_multi_phase_core_projection: TestFixtureWithCoreProjection
    {
        private FakeCheckpointManager _checkpointManager;
        private FakeProjectionProcessingPhase _phase1;
        private FakeProjectionProcessingPhase _phase2;

        class FakeProjectionProcessingStrategy : ProjectionProcessingStrategy
        {
            private readonly FakeProjectionProcessingPhase _phase1;
            private readonly FakeProjectionProcessingPhase _phase2;

            public FakeProjectionProcessingStrategy(
                string name, ProjectionVersion projectionVersion, ILogger logger, FakeProjectionProcessingPhase phase1,
                FakeProjectionProcessingPhase phase2)
                : base(name, projectionVersion, logger)
            {
                _phase1 = phase1;
                _phase2 = phase2;
            }

            protected override IQuerySources GetSourceDefinition()
            {
                return new QuerySourcesDefinition()
                {
                    AllStreams = true,
                    AllEvents = true,
                    ByStreams = true,
                    Options = new QuerySourcesDefinitionOptions { }
                };
            }

            public override bool GetStopOnEof()
            {
                return true;
            }

            public override IProjectionProcessingPhase[] CreateProcessingPhases(
                IPublisher publisher, Guid projectionCorrelationId, PartitionStateCache partitionStateCache,
                Action updateStatistics, CoreProjection coreProjection, ProjectionNamesBuilder namingBuilder,
                ITimeProvider timeProvider, IODispatcher ioDispatcher)
            {
                return new[]
                {
                    _phase1,
                    _phase2
                };
            }
        }

        internal class FakeProjectionProcessingPhase : IProjectionProcessingPhase
        {
            private readonly ICoreProjectionCheckpointManager _checkpointManager;
            private bool _initialized;
            private bool _initializedFromCheckpoint;
            private CheckpointTag _initializedFromCheckpointAt;
            private PhaseState _state;
            private Guid _subscriptionId;

            public FakeProjectionProcessingPhase(ICoreProjectionCheckpointManager checkpointManager)
            {
                _checkpointManager = checkpointManager;
            }

            public void Dispose()
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.CommittedEventReceived message)
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.ProgressChanged message)
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.NotAuthorized message)
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.EofReached message)
            {
                throw new NotImplementedException();
            }

            public void Handle(EventReaderSubscriptionMessage.CheckpointSuggested message)
            {
                throw new NotImplementedException();
            }

            public void Handle(CoreProjectionManagementMessage.GetState message)
            {
                throw new NotImplementedException();
            }

            public void Handle(CoreProjectionManagementMessage.GetResult message)
            {
                throw new NotImplementedException();
            }

            public void Initialize()
            {
                _initialized = true;
            }

            public void InitializeFromCheckpoint(CheckpointTag checkpointTag)
            {
                _initializedFromCheckpoint = true;
                _initializedFromCheckpointAt = checkpointTag;
            }

            public void Unsubscribed()
            {
                throw new NotImplementedException();
            }

            public void SetState(PhaseState state)
            {
                _state = state;
            }

            public void SetFaulted()
            {
                throw new Exception("Faulted");
            }

            public void ProcessEvent()
            {
                throw new NotImplementedException();
            }

            public void Subscribed(Guid subscriptionId)
            {
                _subscriptionId = subscriptionId;
            }

            public ReaderSubscriptionOptions GetSubscriptionOptions()
            {
                throw new NotImplementedException();
            }

            public ICoreProjectionCheckpointManager CheckpointManager
            {
                get { return _checkpointManager; }
            }

            public IReaderStrategy ReaderStrategy
            {
                get { throw new NotImplementedException(); }
            }

            public bool Initialized
            {
                get { return _initialized; }
            }

            public bool InitializedFromCheckpoint
            {
                get { return _initializedFromCheckpoint; }
            }

            public CheckpointTag InitializedFromCheckpointAt
            {
                get { return _initializedFromCheckpointAt; }
            }

            public PhaseState State
            {
                get { return _state; }
            }

            public Guid SubscriptionId
            {
                get { return _subscriptionId; }
            }

            public void GetStatistics(ProjectionStatistics info)
            {
                throw new NotImplementedException();
            }
        }

        class FakeCheckpointManager : ICoreProjectionCheckpointManager
        {
            private readonly IPublisher _publisher;
            private readonly Guid _projectionCorrelationId;

            private bool _started;
            private CheckpointTag _startedAt;

            public FakeCheckpointManager(IPublisher publisher, Guid projectionCorrelationId)
            {
                _publisher = publisher;
                _projectionCorrelationId = projectionCorrelationId;
            }

            public void Initialize()
            {
            }

            public void Start(CheckpointTag checkpointTag)
            {
                _started = true;
                _startedAt = checkpointTag;
            }

            public void Stopping()
            {
                throw new NotImplementedException();
            }

            public void Stopped()
            {
                throw new NotImplementedException();
            }

            public void GetStatistics(ProjectionStatistics info)
            {
                throw new NotImplementedException();
            }

            public void NewPartition(string partition, CheckpointTag eventCheckpointTag)
            {
                throw new NotImplementedException();
            }

            public void EventsEmitted(EmittedEventEnvelope[] scheduledWrites, Guid causedBy, string correlationId)
            {
                throw new NotImplementedException();
            }

            public void StateUpdated(string partition, PartitionState oldState, PartitionState newState)
            {
                throw new NotImplementedException();
            }

            public void EventProcessed(CheckpointTag checkpointTag, float progress)
            {
                throw new NotImplementedException();
            }

            public bool CheckpointSuggested(CheckpointTag checkpointTag, float progress)
            {
                throw new NotImplementedException();
            }

            public void Progress(float progress)
            {
                throw new NotImplementedException();
            }

            public void BeginLoadState()
            {
                _publisher.Publish(
                    new CoreProjectionProcessingMessage.CheckpointLoaded(
                        _projectionCorrelationId, CheckpointTag.FromPosition(0, 0, -1), ""));
            }

            public void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag)
            {
                _publisher.Publish(
                    new CoreProjectionProcessingMessage.PrerecordedEventsLoaded(_projectionCorrelationId, checkpointTag));
            }

            public void BeginLoadPartitionStateAt(string statePartition, CheckpointTag requestedStateCheckpointTag, Action<PartitionState> loadCompleted)
            {
                throw new NotImplementedException();
            }

            public void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action committed)
            {
                throw new NotImplementedException();
            }

            public CheckpointTag LastProcessedEventPosition
            {
                get { throw new NotImplementedException(); }
            }

            public bool Started
            {
                get { return _started; }
            }

            public CheckpointTag StartedAt
            {
                get { return _startedAt; }
            }
        }

        public ICoreProjectionCheckpointManager CheckpointManager
        {
            get { return _checkpointManager; }
        }

        public FakeProjectionProcessingPhase Phase1
        {
            get { return _phase1; }
        }

        public FakeProjectionProcessingPhase Phase2
        {
            get { return _phase2; }
        }

        protected override ProjectionProcessingStrategy GivenProjectionProcessingStrategy()
        {
            _checkpointManager = new FakeCheckpointManager(_bus, _projectionCorrelationId);
            _phase1 = new FakeProjectionProcessingPhase(CheckpointManager);
            _phase2 = new FakeProjectionProcessingPhase(CheckpointManager);
            return new FakeProjectionProcessingStrategy(
                _projectionName, _version, new ConsoleLogger("logger"), Phase1, Phase2);
        }
    }

}
