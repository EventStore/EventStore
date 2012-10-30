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
using EventStore.Common.Log;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EmittedStream : IHandle<ClientMessage.WriteEventsCompleted>,
                                 IHandle<ClientMessage.ReadStreamEventsBackwardCompleted>
    {
        private readonly ILogger _logger;
        private readonly string _streamId;
        private readonly bool _recoveryMode;
        private readonly IPublisher _publisher;
        private readonly IHandle<ProjectionMessage.Projections.ReadyForCheckpoint> _readyHandler;

        private readonly Queue<Tuple<EmittedEvent[], CheckpointTag>> _pendingWrites =
            new Queue<Tuple<EmittedEvent[], CheckpointTag>>();

        private bool _checkpointRequested;
        private bool _awaitingWriteCompleted;
        private bool _awaitingListEventsCompleted;
        private bool _started;

        private readonly int _maxWriteBatchLength;
        private CheckpointTag _lastCommittedMetadata; // TODO: rename
        private Event[] _submittedToWriteEvents;
        private int _lastKnownEventNumber;


        public EmittedStream(
            string streamId, IPublisher publisher,
            IHandle<ProjectionMessage.Projections.ReadyForCheckpoint> readyHandler, bool recoveryMode,
            int maxWriteBatchLength, ILogger logger = null)
        {
            if (streamId == null) throw new ArgumentNullException("streamId");
            if (publisher == null) throw new ArgumentNullException("publisher");
            if (readyHandler == null) throw new ArgumentNullException("readyHandler");
            if (streamId == "") throw new ArgumentException("streamId");
            _streamId = streamId;
            _recoveryMode = recoveryMode;
            _publisher = publisher;
            _readyHandler = readyHandler;
            _maxWriteBatchLength = maxWriteBatchLength;
            _logger = logger;
        }

        public void EmitEvents(EmittedEvent[] events, CheckpointTag position)
        {
            EnsureCheckpointNotRequested();
            _pendingWrites.Enqueue(Tuple.Create(events, position));
            ProcessWrites();
        }

        public void Checkpoint()
        {
            EnsureStreamStarted();
            EnsureCheckpointNotRequested();
            _checkpointRequested = true;
            ProcessRequestedCheckpoint();
        }

        public void Start()
        {
            EnsureCheckpointNotRequested();
            if (_started)
                throw new InvalidOperationException("Stream is already started");
            _started = true;
            ProcessWrites();
        }

        public int GetWritePendingEvents()
        {
            return _pendingWrites.Count;
        }

        public int GetWritesInProgress()
        {
            return _awaitingWriteCompleted ? 1 : 0;
        }

        public int GetReadsInProgress()
        {
            return _awaitingListEventsCompleted ? 1 : 0;
        }

        public void Handle(ClientMessage.WriteEventsCompleted message)
        {
            if (!_awaitingWriteCompleted)
                throw new InvalidOperationException("WriteEvents has not been submitted");
            if (message.ErrorCode == OperationErrorCode.Success)
            {
                _lastKnownEventNumber = message.EventNumber + _submittedToWriteEvents.Length - 1
                                        + (_lastKnownEventNumber == ExpectedVersion.NoStream ? 1 : 0); // account for stream crated
                OnWriteCompleted();
                return;
            }
            if (_logger != null)
                _logger.Info(
                    "Failed to write events to stream {0}. Error: {1}", message.EventStreamId,
                    Enum.GetName(typeof (OperationErrorCode), message.ErrorCode));
            if (message.ErrorCode == OperationErrorCode.CommitTimeout
                || message.ErrorCode == OperationErrorCode.ForwardTimeout
                || message.ErrorCode == OperationErrorCode.PrepareTimeout
                || message.ErrorCode == OperationErrorCode.WrongExpectedVersion)
            {
                if (_logger != null) _logger.Info("Retrying write to {0}", message.EventStreamId);
                PublishWriteEvents();
            }
            else
                throw new NotSupportedException("Unsupported error code received");
        }

        public void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            if (!_awaitingListEventsCompleted)
                throw new InvalidOperationException("ReadStreamEventsBackward has not been requested");
            _awaitingListEventsCompleted = false;
            if (message.Events.Length == 0)
            {
                _lastCommittedMetadata = null;
                _lastKnownEventNumber = ExpectedVersion.NoStream;
                SubmitWriteEvents();
            }
            else
            {
                var projectionStateMetadata = message.Events[0].Event.Metadata.ParseJson<CheckpointTag>();
                _lastCommittedMetadata = projectionStateMetadata;
                _lastKnownEventNumber = message.Events[0].Event.EventNumber;
                SubmitWriteEventsInRecovery();
            }
        }

        private void ProcessWrites()
        {
            if (_started && !_awaitingWriteCompleted && _pendingWrites.Count > 0)
            {
                _awaitingWriteCompleted = true;

                if (_recoveryMode && _lastCommittedMetadata == null)
                {
                    SubmitListEvents();
                }
                else
                {
                    //TODO: write tests for single read events in recovery only
                    if (_recoveryMode)
                        SubmitWriteEventsInRecovery();
                    else
                        SubmitWriteEvents();
                }
            }
        }

        private void SubmitListEvents()
        {
            _awaitingListEventsCompleted = true;
            _publisher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), new SendToThisEnvelope(this), _streamId, -1, 1, resolveLinks: false));
        }

        private void SubmitWriteEvents()
        {
            var list = new List<Tuple<EmittedEvent[], CheckpointTag>>();
            while (_pendingWrites.Count > 0 && list.Count < _maxWriteBatchLength)
            {
                var eventsToWrite = _pendingWrites.Dequeue();
                list.Add(eventsToWrite);
            }
            var events = from eventsToWrite in list
                         from v in eventsToWrite.Item1
                         let data = v.Data
                         let metadata = CreateSerializedMetadata(eventsToWrite)
                         select new Event(v.EventId, v.EventType, false, data, metadata);

            _submittedToWriteEvents = events.ToArray();
            PublishWriteEvents();
        }

        private void PublishWriteEvents()
        {
            _publisher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), new SendToThisEnvelope(this), RoutingStrategy.AllowForwarding, _streamId,
                    _recoveryMode ? _lastKnownEventNumber : ExpectedVersion.Any, _submittedToWriteEvents));
        }

        private byte[] CreateSerializedMetadata(Tuple<EmittedEvent[], CheckpointTag> eventsToWrite)
        {
            return eventsToWrite.Item2.ToJsonBytes();
        }

        private void EnsureCheckpointNotRequested()
        {
            if (_checkpointRequested)
                throw new InvalidOperationException("Checkpoint requested");
        }

        private void EnsureStreamStarted()
        {
            if (!_started)
                throw new InvalidOperationException("Not started");
        }

        private void OnWriteCompleted()
        {
            _awaitingWriteCompleted = false;
            ProcessWrites();
            ProcessRequestedCheckpoint();
        }

        private void ProcessRequestedCheckpoint()
        {
            if (_checkpointRequested && !_awaitingWriteCompleted && _pendingWrites.Count == 0)
            {
                _readyHandler.Handle(new ProjectionMessage.Projections.ReadyForCheckpoint());
            }
        }

        private void SubmitWriteEventsInRecovery()
        {
            while (_pendingWrites.Count > 0)
            {
                var eventsToWrite = _pendingWrites.Peek();
                if (eventsToWrite.Item2 > _lastCommittedMetadata)
                {
                    SubmitWriteEvents();
                    return;
                }
                _pendingWrites.Dequeue(); // drop already committed event
            }
            OnWriteCompleted();
        }
    }
}
