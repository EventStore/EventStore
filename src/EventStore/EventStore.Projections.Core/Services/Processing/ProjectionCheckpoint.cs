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
using EventStore.Common.Log;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ProjectionCheckpoint : IDisposable, IEmittedStreamContainer, IEventWriter
    {
        private readonly int _maxWriteBatchLength;
        private readonly ILogger _logger;

        private readonly Dictionary<string, EmittedStream> _emittedStreams = new Dictionary<string, EmittedStream>();
        private readonly IPrincipal _runAs;
        private readonly CheckpointTag _from;
        private CheckpointTag _last;
        private readonly IProjectionCheckpointManager _readyHandler;
        private readonly PositionTagger _positionTagger;

        private bool _checkpointRequested = false;
        private int _requestedCheckpoints;
        private bool _started = false;
        private readonly CheckpointTag _zero;

        private readonly IODispatcher _ioDispatcher;

        private readonly ProjectionVersion _projectionVersion;

        private List<IEnvelope> _awaitingStreams;

        public ProjectionCheckpoint(
            IODispatcher ioDispatcher, ProjectionVersion projectionVersion, IPrincipal runAs,
            IProjectionCheckpointManager readyHandler, CheckpointTag from, PositionTagger positionTagger,
            CheckpointTag zero, int maxWriteBatchLength, ILogger logger = null)
        {
            if (ioDispatcher == null) throw new ArgumentNullException("ioDispatcher");
            if (readyHandler == null) throw new ArgumentNullException("readyHandler");
            if (positionTagger == null) throw new ArgumentNullException("positionTagger");
            if (zero == null) throw new ArgumentNullException("zero");
            if (from.CommitPosition < from.PreparePosition) throw new ArgumentException("from");
            //NOTE: fromCommit can be equal fromPrepare on 0 position.  Is it possible anytime later? Ignoring for now.
            _ioDispatcher = ioDispatcher;
            _projectionVersion = projectionVersion;
            _runAs = runAs;
            _readyHandler = readyHandler;
            _positionTagger = positionTagger;
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

        public void ValidateOrderAndEmitEvents(EmittedEventEnvelope[] events)
        {
            UpdateLastPosition(events);
            EnsureCheckpointNotRequested();

            var groupedEvents = events.GroupBy(v => v.Event.StreamId);
            foreach (var eventGroup in groupedEvents)
            {
                EmitEventsToStream(eventGroup.Key, eventGroup.ToArray());
            }
        }

        private void UpdateLastPosition(EmittedEventEnvelope[] events)
        {
            foreach (var emittedEvent in events)
            {
                if (emittedEvent.Event.CausedByTag > _last)
                    _last = emittedEvent.Event.CausedByTag;
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

        private void EmitEventsToStream(string streamId, EmittedEventEnvelope[] emittedEvents)
        {
            EmittedStream stream;
            if (!_emittedStreams.TryGetValue(streamId, out stream))
            {
                var streamMetadata = emittedEvents.Length > 0 ? emittedEvents[0].StreamMetadata : null;

                var writerConfiguration = new EmittedStream.WriterConfiguration(
                    streamMetadata, _runAs, maxWriteBatchLength: _maxWriteBatchLength, logger: _logger);

                stream = new EmittedStream(
                    streamId, writerConfiguration, _projectionVersion, _positionTagger, _from, _ioDispatcher, this);

                if (_started)
                    stream.Start();
                _emittedStreams.Add(streamId, stream);
            }
            stream.EmitEvents(emittedEvents.Select(v => v.Event).ToArray());
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

        public void Handle(CoreProjectionProcessingMessage.Failed message)
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
            if (_awaitingStreams == null)
                _awaitingStreams = new List<IEnvelope>();
            _awaitingStreams.Add(message.Envelope);
        }

        public void Handle(CoreProjectionProcessingMessage.EmittedStreamWriteCompleted message)
        {
            var awaitingStreams = _awaitingStreams;
            _awaitingStreams = null; // still awaiting will re-register
            if (awaitingStreams != null)
                foreach (var stream in awaitingStreams)
                    stream.ReplyWith(message);
        }
    }
}
