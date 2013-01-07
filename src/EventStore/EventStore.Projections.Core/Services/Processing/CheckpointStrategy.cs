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
using EventStore.Core.Bus;
using System.Linq;
using EventStore.Core.Messages;
using EventStore.Core.Services.TimerService;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services.Processing
{
    public class CheckpointStrategy
    {
        private readonly bool _allStreams;
        private readonly HashSet<string> _categories;
        private readonly HashSet<string> _streams;
        private readonly bool _allEvents;
        private readonly HashSet<string> _events;
        private readonly bool _byStream;
        private readonly bool _byCustomPartitions;
        private readonly bool _useEventIndexes;
        private readonly bool _reorderEvents;
        private readonly int _processingLag;
        private readonly bool _emitStateUpdated;
        private readonly EventFilter _eventFilter;
        private readonly PositionTagger _positionTagger;
        private readonly bool _useCheckpoints;

        public class Builder : QuerySourceProcessingStrategyBuilder
        {
            public CheckpointStrategy Build(ProjectionConfig config)
            {
                base.Validate(config);
                return new CheckpointStrategy(
                    _allStreams, ToSet(_categories), ToSet(_streams), _allEvents, ToSet(_events), _byStream,
                    _byCustomPartitions, _options.UseEventIndexes, _options.ReorderEvents, _options.ProcessingLag,
                    _options.EmitStateUpdated, config.CheckpointsEnabled);
            }
        }

        public EventFilter EventFilter
        {
            get { return _eventFilter; }
        }

        public PositionTagger PositionTagger
        {
            get { return _positionTagger; }
        }

        public bool EmitStateUpdated
        {
            get { return _emitStateUpdated; }
        }

        public bool UseCheckpoints
        {
            get { return _useCheckpoints; }
        }

        public bool IsEmiEnabled()
        {
            return _streams == null || _streams.Count <= 1;
        }

        public EventReader CreatePausedEventReader(Guid eventReaderId, IPublisher publisher, CheckpointTag checkpointTag, bool stopOnEof)
        {
            if (_allStreams && _useEventIndexes && _events != null && _events.Count == 1)
            {
                var streamName = checkpointTag.Streams.Keys.First();
                return CreatePausedStreamEventReader(
                    eventReaderId, publisher, checkpointTag, streamName, stopOnEof, resolveLinkTos: true);
            }
            if (_allStreams && _useEventIndexes && _events != null && _events.Count > 1)
            {
                return CreatePausedMultiStreamEventReader(
                    eventReaderId, publisher, checkpointTag, stopOnEof, resolveLinkTos: true, streams: GetEventIndexStreams());
            }
            if (_allStreams)
            {
                var eventReader = new TransactionFileEventReader(
                    publisher, eventReaderId,
                    new EventPosition(checkpointTag.CommitPosition.Value, checkpointTag.PreparePosition.Value),
                    new RealTimeProvider(), deliverEndOfTFPosition: true, stopOnEof: stopOnEof);
                return eventReader;
            }
            if (_streams != null && _streams.Count == 1)
            {
                var streamName = checkpointTag.Streams.Keys.First();
                //TODO: handle if not the same
                return CreatePausedStreamEventReader(
                    eventReaderId, publisher, checkpointTag, streamName, stopOnEof, resolveLinkTos: true);
            }
            if (_categories != null && _categories.Count == 1)
            {
                var streamName = checkpointTag.Streams.Keys.First();
                return CreatePausedStreamEventReader(
                    eventReaderId, publisher, checkpointTag, streamName, stopOnEof, resolveLinkTos: true);
            }
            if (_streams != null && _streams.Count > 1)
            {
                return CreatePausedMultiStreamEventReader(
                    eventReaderId, publisher, checkpointTag, stopOnEof, resolveLinkTos: true, streams: _streams);
            }
            throw new NotSupportedException();
        }

        private static EventReader CreatePausedStreamEventReader(
            Guid eventReaderId, IPublisher publisher, CheckpointTag checkpointTag, string streamName, bool stopOnEof,
            bool resolveLinkTos)
        {
            var lastProcessedSequenceNumber = checkpointTag.Streams.Values.First();
            var fromSequenceNumber = lastProcessedSequenceNumber + 1;
            var eventReader = new StreamEventReader(
                publisher, eventReaderId, streamName, fromSequenceNumber, new RealTimeProvider(), resolveLinkTos,
                stopOnEof);
            return eventReader;
        }

        private EventReader CreatePausedMultiStreamEventReader(
            Guid eventReaderId, IPublisher publisher, CheckpointTag checkpointTag, bool stopOnEof, bool resolveLinkTos,
            IEnumerable<string> streams)
        {
            var nextPositions = checkpointTag.Streams.ToDictionary(v => v.Key, v => v.Value + 1);

            var eventReader = new MultiStreamEventReader(
                publisher, eventReaderId, streams.ToArray(), nextPositions, resolveLinkTos, new RealTimeProvider(),
                stopOnEof);
            return eventReader;
        }

        private CheckpointStrategy(
            bool allStreams, HashSet<string> categories, HashSet<string> streams, bool allEvents, HashSet<string> events,
            bool byStream, bool byCustomPartitions, bool useEventIndexes, bool reorderEvents, int processingLag,
            bool emitStateUpdated, bool useCheckpoints)
        {
            _allStreams = allStreams;
            _categories = categories;
            _streams = streams;
            _allEvents = allEvents;
            _events = events;
            _byStream = byStream;
            _byCustomPartitions = byCustomPartitions;
            _useEventIndexes = useEventIndexes;
            _reorderEvents = reorderEvents;
            _processingLag = processingLag;
            _emitStateUpdated = emitStateUpdated;
            _useCheckpoints = useCheckpoints;

            _eventFilter = CreateEventFilter();
            _positionTagger = CreatePositionTagger();
        }

        private EventFilter CreateEventFilter()
        {
            if (_allStreams && _useEventIndexes && _events != null && _events.Count == 1)
                return new IndexedEventTypeEventFilter(_events.First());
            if (_allStreams && _useEventIndexes && _events != null && _events.Count > 1)
                return new IndexedEventTypesEventFilter(_events.ToArray());
            if (_allStreams)
                return new TransactionFileEventFilter(_allEvents, _events);
            if (_categories != null && _categories.Count == 1)
                return new CategoryEventFilter(_categories.First(), _allEvents, _events);
            if (_categories != null)
                throw new NotSupportedException();
            if (_streams != null && _streams.Count == 1)
                return new StreamEventFilter(_streams.First(), _allEvents, _events);
            if (_streams != null && _streams.Count > 1)
                return new MultiStreamEventFilter(_streams, _allEvents, _events);
            throw new NotSupportedException();
        }

        private PositionTagger CreatePositionTagger()
        {
            if (_allStreams && _useEventIndexes && _events != null && _events.Count == 1)
                return new StreamPositionTagger("$et-" + _events.First());
            if (_allStreams && _useEventIndexes && _events != null && _events.Count > 1)
                return new MultiStreamPositionTagger(GetEventIndexStreams());
            if (_allStreams && _reorderEvents)
                return new PreparePositionTagger();
            if (_allStreams)
                return new TransactionFilePositionTagger();
            if (_categories != null && _categories.Count == 1)
                //TODO: '-' is a hardcoded separator
                return new StreamPositionTagger("$ce-" + _categories.First());
            if (_categories != null)
                throw new NotSupportedException();
            if (_streams != null && _streams.Count == 1)
                return new StreamPositionTagger(_streams.First());
            if (_streams != null && _streams.Count > 1)
                return new MultiStreamPositionTagger(_streams.ToArray());
            throw new NotSupportedException();
        }

        private string[] GetEventIndexStreams()
        {
            return _events.Select(v => "$et-" + v).ToArray();
        }

        public StatePartitionSelector CreateStatePartitionSelector(IProjectionStateHandler projectionStateHandler)
        {
            return _byCustomPartitions
                       ? new ByHandleStatePartitionSelector(projectionStateHandler)
                       : (_byStream
                              ? (StatePartitionSelector) new ByStreamStatePartitionSelector()
                              : new NoopStatePartitionSelector());
        }

        public ICoreProjectionCheckpointManager CreateCheckpointManager(
            ICoreProjection coreProjection, Guid projectionCorrelationId, IPublisher publisher,
            RequestResponseDispatcher<ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted> readDispatcher,
            RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted> writeDispatcher,
            ProjectionConfig projectionConfig, string name, ProjectionNamesBuilder namingBuilder)
        {
            var emitPartitionCheckpoints = UseCheckpoints && !EmitStateUpdated && (_byCustomPartitions || _byStream);

            if (_allStreams && _useEventIndexes && _events != null && _events.Count > 1)
            {
                return new MultiStreamMultiOutputCheckpointManager(
                    coreProjection, publisher, projectionCorrelationId, readDispatcher, writeDispatcher,
                    projectionConfig, name, PositionTagger, namingBuilder, UseCheckpoints, EmitStateUpdated);
            }
            else if (_streams != null && _streams.Count > 1)
            {
                return new MultiStreamMultiOutputCheckpointManager(
                    coreProjection, publisher, projectionCorrelationId, readDispatcher, writeDispatcher,
                    projectionConfig, name, PositionTagger, namingBuilder, UseCheckpoints, EmitStateUpdated);
            }
            else
            {
                return new DefaultCheckpointManager(
                    coreProjection, publisher, projectionCorrelationId, readDispatcher, writeDispatcher,
                    projectionConfig, name, PositionTagger, namingBuilder, UseCheckpoints,
                    EmitStateUpdated, emitPartitionCheckpoints);
            }
        }

        public IProjectionSubscription CreateProjectionSubscription(
            CheckpointTag fromCheckpointTag, Guid projectionCorrelationId, Guid subscriptionId,
            ICoreProjection projection, long checkpointUnhandledBytesThreshold, bool stopOnEof)
        {
            if (_reorderEvents)
                return new EventReorderingProjectionSubscription(
                    projectionCorrelationId, subscriptionId, fromCheckpointTag, projection, projection, projection,
                    projection, this, checkpointUnhandledBytesThreshold, _processingLag, stopOnEof);
            else
                return new ProjectionSubscription(
                    projectionCorrelationId, subscriptionId, fromCheckpointTag, projection, projection, projection,
                    projection, this, checkpointUnhandledBytesThreshold, stopOnEof);
        }
    }
}
