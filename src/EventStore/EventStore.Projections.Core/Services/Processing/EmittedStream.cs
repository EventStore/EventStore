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
using EventStore.Common.Log;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class EmittedStream : IDisposable
    {
        private readonly
            RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;


        private readonly ILogger _logger;
        private readonly string _streamId;
        private readonly CheckpointTag _zeroPosition;
        private readonly IProjectionCheckpointManager _readyHandler;

        private readonly Queue<EmittedEvent[]> _pendingWrites =
            new Queue<EmittedEvent[]>();

        private bool _checkpointRequested;
        private bool _awaitingWriteCompleted;
        private bool _awaitingListEventsCompleted;
        private bool _started;

        private readonly int _maxWriteBatchLength;
        private CheckpointTag _lastSubmittedOrCommittedMetadata; // TODO: rename
        private Event[] _submittedToWriteEvents;
        private EmittedEvent[] _submittedToWriteEmittedEvents;
        private int _lastKnownEventNumber = ExpectedVersion.Invalid;
        private readonly bool _noCheckpoints;
        private bool _disposed;


        public EmittedStream(
            string streamId, CheckpointTag zeroPosition,
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            IProjectionCheckpointManager readyHandler, int maxWriteBatchLength, ILogger logger = null,
            bool noCheckpoints = false)
        {
            if (streamId == null) throw new ArgumentNullException("streamId");
            if (readDispatcher == null) throw new ArgumentNullException("readDispatcher");
            if (writeDispatcher == null) throw new ArgumentNullException("writeDispatcher");
            if (readyHandler == null) throw new ArgumentNullException("readyHandler");
            if (streamId == "") throw new ArgumentException("streamId");
            _streamId = streamId;
            _zeroPosition = zeroPosition;
            _readDispatcher = readDispatcher;
            _writeDispatcher = writeDispatcher;
            _readyHandler = readyHandler;
            _maxWriteBatchLength = maxWriteBatchLength;
            _logger = logger;
            _noCheckpoints = noCheckpoints;
        }

        public void EmitEvents(EmittedEvent[] events)
        {
            if (events == null) throw new ArgumentNullException("events");
            foreach (var @event in events)
                if (@event.StreamId != _streamId)
                    throw new ArgumentException("Invalid streamId", "events");
            EnsureCheckpointNotRequested();
            _pendingWrites.Enqueue(events);
            ProcessWrites();
        }

        public void Checkpoint()
        {
            EnsureCheckpointsEnabled();
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

        private void Handle(ClientMessage.WriteEventsCompleted message)
        {
            if (!_awaitingWriteCompleted)
                throw new InvalidOperationException("WriteEvents has not been submitted");
            if (_disposed)
                return;
            if (message.Result == OperationResult.Success)
            {
                _lastKnownEventNumber = message.FirstEventNumber + _submittedToWriteEvents.Length - 1
                                        + (_lastKnownEventNumber == ExpectedVersion.NoStream ? 1 : 0); // account for stream crated
                NotifyEventsCommitted(_submittedToWriteEmittedEvents);
                OnWriteCompleted();
                return;
            }
            if (_logger != null)
            {
                _logger.Info("Failed to write events to stream {0}. Error: {1}",
                             _streamId,
                             Enum.GetName(typeof (OperationResult), message.Result));
            }
            switch (message.Result)
            {
                case OperationResult.WrongExpectedVersion:
                    RequestRestart(string.Format("The '{0}' stream has be written to from the outside", _streamId));
                    break;
                case OperationResult.PrepareTimeout:
                case OperationResult.ForwardTimeout:
                case OperationResult.CommitTimeout:
                    if (_logger != null) _logger.Info("Retrying write to {0}", _streamId);
                    PublishWriteEvents();
                    break;
                default:
                    throw new NotSupportedException("Unsupported error code received");
            }
        }

        private void RequestRestart(string reason)
        {
            _readyHandler.Handle(new CoreProjectionProcessingMessage.RestartRequested(reason));
        }

        private void Handle(ClientMessage.ReadStreamEventsBackwardCompleted message)
        {
            if (!_awaitingListEventsCompleted)
                throw new InvalidOperationException("ReadStreamEventsBackward has not been requested");
            if (_disposed)
                return;
            _awaitingListEventsCompleted = false;
            if (message.Events.Length == 0)
            {
                _lastSubmittedOrCommittedMetadata = _zeroPosition;
                _lastKnownEventNumber = ExpectedVersion.NoStream;
                SubmitWriteEvents();
            }
            else
            {
                var projectionStateMetadata = message.Events[0].Event.Metadata.ParseJson<CheckpointTag>();
                _lastSubmittedOrCommittedMetadata = projectionStateMetadata;
                _lastKnownEventNumber = message.Events[0].Event.EventNumber;
                SubmitWriteEventsInRecovery();
            }
        }

        private void ProcessWrites()
        {
            if (_started && !_awaitingWriteCompleted && _pendingWrites.Count > 0)
            {
                _awaitingWriteCompleted = true;

                if (_lastSubmittedOrCommittedMetadata == null)
                    SubmitListEvents();
                else
                    SubmitWriteEventsInRecovery();
            }
        }

        private void SubmitListEvents()
        {
            _awaitingListEventsCompleted = true;
            _readDispatcher.Publish(
                new ClientMessage.ReadStreamEventsBackward(
                    Guid.NewGuid(), _readDispatcher.Envelope, _streamId, -1, 1, resolveLinks: false, validationStreamVersion: null), Handle);
        }

        private void SubmitWriteEvents()
        {
            var events = new List<Event>();
            var emittedEvents = new List<EmittedEvent>();
            while (_pendingWrites.Count > 0 && events.Count < _maxWriteBatchLength)
            {
                var eventsToWrite = _pendingWrites.Dequeue();

                foreach (var e in eventsToWrite)
                {
                    var expectedTag = e.ExpectedTag;
                    var causedByTag = e.CausedByTag;
                    if (expectedTag != null)
                        if (DetectConcurrencyViolations(expectedTag))
                        {
                            RequestRestart(
                                string.Format(
                                    "Wrong expected tag while submitting write event request to the '{0}' stream.  The last known stream tag is: '{1}'  the expected tag is: '{2}'",
                                    _streamId, _lastSubmittedOrCommittedMetadata, expectedTag));
                            return;
                        }
                    _lastSubmittedOrCommittedMetadata = causedByTag;
                    events.Add(new Event(e.EventId, e.EventType, true, e.Data, e.CausedByTag.ToJsonBytes()));
                    emittedEvents.Add(e);
                }
            }
            _submittedToWriteEvents = events.ToArray();
            _submittedToWriteEmittedEvents = emittedEvents.ToArray();

            PublishWriteEvents();
        }

        private bool DetectConcurrencyViolations(CheckpointTag expectedTag)
        {
            //NOTE: the following condition is only meant to detect concurrency violations when
            // another instance of the projection (running in the another node etc) has been writing to 
            // the same stream.  However, the expected tag sometimes can be greater than last actually written tag
            // This happens when a projection is restarted from a checkpoint and the checkpoint has been made at 
            // position not updating the projection state 
            return expectedTag < _lastSubmittedOrCommittedMetadata;
        }

        private void PublishWriteEvents()
        {
            _writeDispatcher.Publish(
                new ClientMessage.WriteEvents(
                    Guid.NewGuid(), _writeDispatcher.Envelope, true, _streamId,
                    _lastKnownEventNumber, _submittedToWriteEvents), Handle);
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
                EnsureCheckpointsEnabled();
                _readyHandler.Handle(new CoreProjectionProcessingMessage.ReadyForCheckpoint(this));
            }
        }

        private void EnsureCheckpointsEnabled()
        {
            if (_noCheckpoints)
                throw new InvalidOperationException("Checkpoints disabled");
        }

        private void SubmitWriteEventsInRecovery()
        {
            while (_pendingWrites.Count > 0)
            {
                var eventsToWrite = _pendingWrites.Peek();
                if (eventsToWrite[0].CausedByTag > _lastSubmittedOrCommittedMetadata)
                {
                    SubmitWriteEvents();
                    return;
                }
                
                NotifyEventsCommitted(eventsToWrite);
                _pendingWrites.Dequeue(); // drop already committed event
            }
            OnWriteCompleted();
        }

        private static void NotifyEventsCommitted(EmittedEvent[] eventsToWrite)
        {
            foreach (var @event in eventsToWrite)
                if (@event.OnCommitted != null)
                    @event.OnCommitted();
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }
}
