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
using System.Collections;
using System.Collections.Generic;
using EventStore.Common.Log;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionCheckpoint : IDisposable, IEmittedStreamContainer, IEventWriter
    {
        private readonly int _maxWriteBatchLength;
        private readonly ILogger _logger;

        private readonly Dictionary<string, EmittedStream> _emittedStreams = new Dictionary<string, EmittedStream>();
        private readonly CheckpointTag _from;
        private CheckpointTag _last;
        private readonly IProjectionCheckpointManager _readyHandler;

        private bool _checkpointRequested = false;
        private int _requestedCheckpoints;
        private bool _started = false;
        private readonly CheckpointTag _zero;

        private readonly RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private readonly RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private List<EmittedStream> _awaitingStreams;

        public ProjectionCheckpoint(RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            IProjectionCheckpointManager readyHandler, CheckpointTag from, CheckpointTag zero, int maxWriteBatchLength,
            ILogger logger = null)
        {
            if (readDispatcher == null) throw new ArgumentNullException("readDispatcher");
            if (writeDispatcher == null) throw new ArgumentNullException("writeDispatcher");
            if (readyHandler == null) throw new ArgumentNullException("readyHandler");
            if (zero == null) throw new ArgumentNullException("zero");
            if (from.CommitPosition <= from.PreparePosition) throw new ArgumentException("from");
            //NOTE: fromCommit can be equal fromPrepare on 0 position.  Is it possible anytime later? Ignoring for now.
            _readDispatcher = readDispatcher;
            _writeDispatcher = writeDispatcher;
            _readyHandler = readyHandler;
            _zero = zero;
            _from = _last = from;
            _maxWriteBatchLength = maxWriteBatchLength;
            _logger = logger;
        }

        public void Start()
        {
            if (_started)
                throw new InvalidOperationException("Projection has been already started");
            _started = true;
            foreach (var stream in _emittedStreams.Values)
            {
                stream.Start();
            }
        }

        public void ValidateOrderAndEmitEvents(EmittedEvent[] events)
        {
            UpdateLastPosition(events);
            EnsureCheckpointNotRequested();
            
            var groupedEvents = events.GroupBy(v => v.StreamId);
            foreach (var eventGroup in groupedEvents)
            {
                EmitEventsToStream(eventGroup.Key, eventGroup.ToArray());
            }
        }

        private void UpdateLastPosition(EmittedEvent[] events)
        {
            foreach (var emittedEvent in events)
            {
                if (emittedEvent.CausedByTag > _last) 
                    _last = emittedEvent.CausedByTag;
            }
        }

        private void ValidateCheckpointPosition(CheckpointTag position)
        {
            if (position <= _from)
                throw new InvalidOperationException(
                    string.Format(
                        "Checkpoint position before or equal to the checkpoint start position. Requested: '{0}' Started: '{1}'",
                        position, _from));
            if (position < _last)
                throw new InvalidOperationException(
                    string.Format(
                        "Checkpoint position before last handled position. Requested: '{0}' Last: '{1}'", position,
                        _last));
        }

        public void Prepare(CheckpointTag position)
        {
            if (!_started)
                throw new InvalidOperationException("Projection has not been started");
            ValidateCheckpointPosition(position);
            _checkpointRequested = true;
            _requestedCheckpoints = 1; // avoid multiple checkpoint ready messages if already ready
            foreach (var emittedStream in _emittedStreams.Values)
            {
                _requestedCheckpoints++;
                emittedStream.Checkpoint();
            }
            _requestedCheckpoints--;
            OnCheckpointCompleted();
        }

        private void EnsureCheckpointNotRequested()
        {
            if (_checkpointRequested)
                throw new InvalidOperationException("Checkpoint requested");
        }

        private void EmitEventsToStream(
            string streamId, EmittedEvent[] emittedEvents)
        {
            EmittedStream stream;
            if (!_emittedStreams.TryGetValue(streamId, out stream))
            {
                stream = new EmittedStream(
                    streamId, _zero, _from, _readDispatcher, _writeDispatcher, this /*_recoveryMode*/, maxWriteBatchLength: _maxWriteBatchLength,
                    logger: _logger);
                if (_started)
                    stream.Start();
                _emittedStreams.Add(streamId, stream);
            }
            stream.EmitEvents(emittedEvents);
        }

        public void Handle(CoreProjectionProcessingMessage.ReadyForCheckpoint message)
        {
            _requestedCheckpoints--;
            OnCheckpointCompleted();
        }

        private void OnCheckpointCompleted()
        {
            if (_requestedCheckpoints == 0)
            {
                _readyHandler.Handle(new CoreProjectionProcessingMessage.ReadyForCheckpoint(this));
            }
        }

        public int GetWritePendingEvents()
        {
            return _emittedStreams.Values.Sum(v => v.GetWritePendingEvents());
        }

        public int GetWritesInProgress()
        {
            return _emittedStreams.Values.Sum(v => v.GetWritesInProgress());
        }

        public int GetReadsInProgress()
        {
            return _emittedStreams.Values.Sum(v => v.GetReadsInProgress());
        }

        public void Handle(CoreProjectionProcessingMessage.RestartRequested message)
        {
            _readyHandler.Handle(message);
        }

        public void Dispose()
        {
            if (_emittedStreams != null)
                foreach (var stream in _emittedStreams.Values)
                    stream.Dispose();

        }

        public void Handle(CoreProjectionProcessingMessage.EmittedStreamAwaiting message)
        {
            _awaitingStreams.Add(_emittedStreams[message.StreamId]);
        }

        public void Handle(CoreProjectionProcessingMessage.EmittedStreamWriteCompleted message)
        {
            var awaitingStreams = _awaitingStreams;
            _awaitingStreams = null; // still awaiting will re-register
            if (awaitingStreams != null)
                foreach (var stream in awaitingStreams)
                    stream.RetryAwaitingWrites();
        }
    }
}