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
using EventStore.Core.Services;
using EventStore.Core.Services.UserManagement;

namespace EventStore.Projections.Core.Services.Processing
{
    public class DefaultCheckpointManager : CoreProjectionCheckpointManager
    {
        private readonly string _projectionCheckpointStreamId;
        private readonly IPrincipal _runAs;
        private int _inCheckpointWriteAttempt;
        private int _lastWrittenCheckpointEventNumber;
        private int _nextStateIndexToRequest;
        private Event _checkpointEventToBePublished;
        private CheckpointTag _requestedCheckpointPosition;
        private Guid _writeRequestId;
        private Guid _readRequestId;
        private readonly CheckpointTag _zeroTag;
        private int _readRequestsInProgress;
        private readonly HashSet<Guid> _loadStateRequests = new HashSet<Guid>();

        protected readonly ProjectionVersion _projectionVersion;
        protected readonly IODispatcher _ioDispatcher;
        private readonly PositionTagger _positionTagger;

        public DefaultCheckpointManager(
            IPublisher publisher, Guid projectionCorrelationId, ProjectionVersion projectionVersion, IPrincipal runAs,
            IODispatcher ioDispatcher, ProjectionConfig projectionConfig, string name, PositionTagger positionTagger,
            ProjectionNamesBuilder namingBuilder, IResultEmitter resultEmitter, bool useCheckpoints,
            bool emitPartitionCheckpoints = false)
            : base(
                publisher, projectionCorrelationId, projectionConfig, name, positionTagger, namingBuilder, resultEmitter,
                useCheckpoints, emitPartitionCheckpoints)
        {
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            _projectionVersion = projectionVersion;
            _runAs = runAs;
            _ioDispatcher = ioDispatcher;
            _positionTagger = positionTagger;
            _projectionCheckpointStreamId = namingBuilder.MakeCheckpointStreamName();
            _zeroTag = positionTagger.MakeZeroCheckpointTag();
        }

        protected override void BeginWriteCheckpoint(
            CheckpointTag requestedCheckpointPosition, string requestedCheckpointState)
        {
            _requestedCheckpointPosition = requestedCheckpointPosition;
            _inCheckpointWriteAttempt = 1;
            //TODO: pass correct expected version
            _checkpointEventToBePublished = new Event(
                Guid.NewGuid(), ProjectionNamesBuilder.EventType_ProjectionCheckpoint, true,
                requestedCheckpointState == null ? null : Helper.UTF8NoBom.GetBytes(requestedCheckpointState),
                requestedCheckpointPosition.ToJsonBytes(projectionVersion: _projectionVersion));
            PublishWriteStreamMetadataAndCheckpointEvent();
        }

        public override void RecordEventOrder(
            ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action committed)
        {
            committed();
        }

        private void WriteCheckpointEventCompleted(
            string eventStreamId, OperationResult operationResult, int firstWrittenEventNumber)
        {
            EnsureStarted();
            if (_inCheckpointWriteAttempt == 0)
                throw new InvalidOperationException();
            if (operationResult == OperationResult.Success)
            {
                if (_logger != null)
                    _logger.Trace(
                        "Checkpoint has be written for projection {0} at sequence number {1} (current)", _name,
                        firstWrittenEventNumber);
                _lastWrittenCheckpointEventNumber = firstWrittenEventNumber;

                _inCheckpointWriteAttempt = 0;
                CheckpointWritten();
            }
            else
            {
                if (_logger != null)
                {
                    _logger.Info(
                        "Failed to write projection checkpoint to stream {0}. Error: {1}", eventStreamId,
                        Enum.GetName(typeof (OperationResult), operationResult));
                }
                switch (operationResult)
                {
                    case OperationResult.WrongExpectedVersion:
                        RequestRestart("Checkpoint stream has been written to from the outside");
                        break;
                    case OperationResult.PrepareTimeout:
                    case OperationResult.ForwardTimeout:
                    case OperationResult.CommitTimeout:
                        if (_logger != null) _logger.Info("Retrying write checkpoint to {0}", eventStreamId);
                        _inCheckpointWriteAttempt++;
                        PublishWriteStreamMetadataAndCheckpointEvent();
                        break;
                    default:
                        throw new NotSupportedException("Unsupported error code received");
                }
            }
        }

        private void PublishWriteStreamMetadataAndCheckpointEvent()
        {
            if (_logger != null)
                _logger.Trace(
                    "Writing checkpoint for {0} at {1} with expected version number {2}", _name,
                    _requestedCheckpointPosition, _lastWrittenCheckpointEventNumber);
            if (_lastWrittenCheckpointEventNumber == ExpectedVersion.NoStream)
                PublishWriteStreamMetadata();
            else
                PublishWriteCheckpointEvent();
        }

        private void PublishWriteStreamMetadata()
        {
            var metaStreamId = SystemStreams.MetastreamOf(_projectionCheckpointStreamId);
            _writeRequestId = _ioDispatcher.WriteEvent(
                metaStreamId, ExpectedVersion.Any, CreateStreamMetadataEvent(), SystemAccount.Principal, msg =>
                {
                    switch (msg.Result)
                    {
                        case OperationResult.Success:
                            PublishWriteCheckpointEvent();
                            break;
                        default:
                            WriteCheckpointEventCompleted(metaStreamId, msg.Result, ExpectedVersion.Invalid);
                            break;
                    }
                });
        }

        private Event CreateStreamMetadataEvent()
        {
            var eventId = Guid.NewGuid();
            var acl = new StreamAcl(
                readRole: SystemUserGroups.Admins, writeRole: SystemUserGroups.Admins,
                deleteRole: SystemUserGroups.Admins, metaReadRole: SystemUserGroups.All,
                metaWriteRole: SystemUserGroups.Admins);
            var metadata = new StreamMetadata(maxCount: 2, maxAge: null, cacheControl: null, acl: acl);
            var dataBytes = metadata.ToJsonBytes();
            return new Event(eventId, SystemEventTypes.StreamMetadata, isJson: true, data: dataBytes, metadata: null);
        }

        private void PublishWriteCheckpointEvent()
        {
            _writeRequestId = _ioDispatcher.WriteEvent(
                _projectionCheckpointStreamId, _lastWrittenCheckpointEventNumber, _checkpointEventToBePublished,
                SystemAccount.Principal,
                msg => WriteCheckpointEventCompleted(_projectionCheckpointStreamId, msg.Result, msg.FirstEventNumber));
        }

        public override void Initialize()
        {
            base.Initialize();
            _ioDispatcher.Writer.Cancel(_writeRequestId);
            _ioDispatcher.BackwardReader.Cancel(_readRequestId);
            foreach (var requestId in _loadStateRequests)
                _ioDispatcher.BackwardReader.Cancel(requestId);
            _loadStateRequests.Clear();
            _inCheckpointWriteAttempt = 0;
            _lastWrittenCheckpointEventNumber = ExpectedVersion.Invalid;
            _nextStateIndexToRequest = 0;
            _checkpointEventToBePublished = null;
            _requestedCheckpointPosition = null;
            _readRequestsInProgress = 0;
        }

        public override void GetStatistics(ProjectionStatistics info)
        {
            base.GetStatistics(info);
            info.ReadsInProgress += _readRequestsInProgress;
            info.WritesInProgress = ((_inCheckpointWriteAttempt != 0) ? 1 : 0) + info.WritesInProgress;
            info.CheckpointStatus = _inCheckpointWriteAttempt > 0
                ? "Writing (" + _inCheckpointWriteAttempt + ")"
                : info.CheckpointStatus;
        }

        protected override EmittedEventEnvelope[] RegisterNewPartition(string partition, CheckpointTag at)
        {
            return new[]
            {
                new EmittedEventEnvelope(
                    new EmittedDataEvent(
                        _namingBuilder.GetPartitionCatalogStreamName(), Guid.NewGuid(), "$partition", partition, null,
                        at, null))
            };
        }

        protected override void BeforeBeginLoadState()
        {
            _lastWrittenCheckpointEventNumber = ExpectedVersion.NoStream;
            _nextStateIndexToRequest = -1; // from the end
        }

        protected override void RequestLoadState()
        {
            const int recordsToRequest = 10;
            _readRequestId = _ioDispatcher.ReadBackward(
                _projectionCheckpointStreamId, _nextStateIndexToRequest, recordsToRequest, false,
                SystemAccount.Principal, OnLoadStateReadRequestCompleted);
        }

        private void OnLoadStateReadRequestCompleted(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            if (message.Events.Length > 0)
            {
                var checkpoint =
                    message.Events.FirstOrDefault(
                        v => v.Event.EventType == ProjectionNamesBuilder.EventType_ProjectionCheckpoint).Event;
                if (checkpoint != null)
                {
                    var parsed = checkpoint.Metadata.ParseCheckpointTagVersionExtraJson(_projectionVersion);
                    if (parsed.Version.ProjectionId != _projectionVersion.ProjectionId
                        || _projectionVersion.Epoch > parsed.Version.Version)
                    {
                        _lastWrittenCheckpointEventNumber = checkpoint.EventNumber;
                        CheckpointLoaded(null, null);
                    }
                    else
                    {
                        //TODO: check epoch and correctly set _lastWrittenCheckpointEventNumber
                        var checkpointData = Helper.UTF8NoBom.GetString(checkpoint.Data);
                        _lastWrittenCheckpointEventNumber = checkpoint.EventNumber;
                        var adjustedTag = parsed.AdjustBy(_positionTagger, _projectionVersion);
                        CheckpointLoaded(adjustedTag, checkpointData);
                    }
                    return;
                }
            }

            if (message.NextEventNumber != -1)
            {
                _nextStateIndexToRequest = message.NextEventNumber;
                RequestLoadState();
                return;
            }
            _lastWrittenCheckpointEventNumber = ExpectedVersion.NoStream;
            CheckpointLoaded(null, null);
        }

        protected override void BeginLoadPrerecordedEvents(CheckpointTag checkpointTag)
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
    }
}
