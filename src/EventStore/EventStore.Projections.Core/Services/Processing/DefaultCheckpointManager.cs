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
using System.Security.Principal;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class DefaultCheckpointManager : CoreProjectionCheckpointManager,
        IHandle<CoreProjectionCheckpointWriterMessage.CheckpointWritten>,
        IHandle<CoreProjectionCheckpointWriterMessage.RestartRequested>
    {
        private readonly IPrincipal _runAs;
        private readonly CheckpointTag _zeroTag;
        private int _readRequestsInProgress;
        private readonly HashSet<Guid> _loadStateRequests = new HashSet<Guid>();

        protected readonly ProjectionVersion _projectionVersion;
        protected readonly IODispatcher _ioDispatcher;
        private readonly PositionTagger _positionTagger;

        public DefaultCheckpointManager(
            IPublisher publisher, Guid projectionCorrelationId, ProjectionVersion projectionVersion, IPrincipal runAs,
            IODispatcher ioDispatcher, ProjectionConfig projectionConfig, string name, PositionTagger positionTagger,
            ProjectionNamesBuilder namingBuilder, bool useCheckpoints, bool outputRunningResults,
            CoreProjectionCheckpointWriter coreProjectionCheckpointWriter)
            : base(
                publisher, projectionCorrelationId, projectionConfig, name, positionTagger, namingBuilder,
                useCheckpoints, outputRunningResults, coreProjectionCheckpointWriter)
        {
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            _projectionVersion = projectionVersion;
            _runAs = runAs;
            _ioDispatcher = ioDispatcher;
            _positionTagger = positionTagger;
            _zeroTag = positionTagger.MakeZeroCheckpointTag();
        }

        protected override void BeginWriteCheckpoint(
            CheckpointTag requestedCheckpointPosition, string requestedCheckpointState)
        {
            _requestedCheckpointPosition = requestedCheckpointPosition;
            _coreProjectionCheckpointWriter.BeginWriteCheckpoint(
                new SendToThisEnvelope(this), requestedCheckpointPosition, requestedCheckpointState);
        }

        public override void RecordEventOrder(
            ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action committed)
        {
            committed();
        }

        public override void Initialize()
        {
            base.Initialize();
            foreach (var requestId in _loadStateRequests)
                _ioDispatcher.BackwardReader.Cancel(requestId);
            _loadStateRequests.Clear();
            _coreProjectionCheckpointWriter.Initialize();
            _requestedCheckpointPosition = null;
            _readRequestsInProgress = 0;
        }

        public override void GetStatistics(ProjectionStatistics info)
        {
            base.GetStatistics(info);
            info.ReadsInProgress += _readRequestsInProgress;
            _coreProjectionCheckpointWriter.GetStatistics(info);
        }

        protected override EmittedEventEnvelope[] RegisterNewPartition(string partition, CheckpointTag at)
        {
            return new[]
            {
                new EmittedEventEnvelope(
                    new EmittedDataEvent(
                        _namingBuilder.GetPartitionCatalogStreamName(), Guid.NewGuid(), "$partition", false, partition,
                        null, at, null))
            };
        }

        public override void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag)
        {
            PrerecordedEventsLoaded(checkpointTag);
        }

        public override void BeginLoadPartitionStateAt(
            string statePartition, CheckpointTag requestedStateCheckpointTag, Action<PartitionState> loadCompleted)
        {
            var stateEventType = ProjectionNamesBuilder.EventType_PartitionCheckpoint;
            var partitionCheckpointStreamName = _namingBuilder.MakePartitionCheckpointStreamName(statePartition);
            _readRequestsInProgress++;
            var requestId = _ioDispatcher.ReadBackward(
                partitionCheckpointStreamName, -1, 1, false, SystemAccount.Principal,
                m =>
                    OnLoadPartitionStateReadStreamEventsBackwardCompleted(
                        m, requestedStateCheckpointTag, loadCompleted, partitionCheckpointStreamName, stateEventType));
            if (requestId != Guid.Empty)
                _loadStateRequests.Add(requestId);
        }

        private void OnLoadPartitionStateReadStreamEventsBackwardCompleted(
            ClientMessage.ReadStreamEventsBackwardCompleted message, CheckpointTag requestedStateCheckpointTag,
            Action<PartitionState> loadCompleted, string partitionStreamName, string stateEventType)
        {
            //NOTE: the following remove may do nothing in tests as completed is raised before we return from publish. 
            _loadStateRequests.Remove(message.CorrelationId);

            _readRequestsInProgress--;
            if (message.Events.Length == 1)
            {
                EventRecord @event = message.Events[0].Event;
                if (@event.EventType == stateEventType)
                {
                    var parsed = @event.Metadata.ParseCheckpointTagVersionExtraJson(_projectionVersion);
                    if (parsed.Version.ProjectionId != _projectionVersion.ProjectionId
                        || _projectionVersion.Epoch > parsed.Version.Version)
                    {
                        var state = new PartitionState("", null, _zeroTag);
                        loadCompleted(state);
                        return;
                    }
                    else
                    {
                        var loadedStateCheckpointTag = parsed.AdjustBy(_positionTagger, _projectionVersion);
                        // always recovery mode? skip until state before current event
                        //TODO: skip event processing in case we know i has been already processed
                        if (loadedStateCheckpointTag < requestedStateCheckpointTag)
                        {
                            var state = PartitionState.Deserialize(
                                Helper.UTF8NoBom.GetString(@event.Data), loadedStateCheckpointTag);
                            loadCompleted(state);
                            return;
                        }
                    }
                }
            }
            if (message.NextEventNumber == -1)
            {
                var state = new PartitionState("", null, _zeroTag);
                loadCompleted(state);
                return;
            }
            _readRequestsInProgress++;
            var requestId = _ioDispatcher.ReadBackward(
                partitionStreamName, message.NextEventNumber, 1, false, SystemAccount.Principal,
                m =>
                    OnLoadPartitionStateReadStreamEventsBackwardCompleted(
                        m, requestedStateCheckpointTag, loadCompleted, partitionStreamName, stateEventType));
            if (requestId != Guid.Empty)
                _loadStateRequests.Add(requestId);
        }

        protected override ProjectionCheckpoint CreateProjectionCheckpoint(CheckpointTag checkpointPosition)
        {
            return new ProjectionCheckpoint(
                _ioDispatcher, _projectionVersion, _runAs, this, checkpointPosition, _positionTagger, _zeroTag,
                _projectionConfig.MaxWriteBatchLength, _logger);
        }

        public void Handle(CoreProjectionCheckpointWriterMessage.CheckpointWritten message)
        {
            CheckpointWritten(message.Position);
        }

        public void Handle(CoreProjectionCheckpointWriterMessage.RestartRequested message)
        {
            RequestRestart(message.Reason);
        }
    }
}
