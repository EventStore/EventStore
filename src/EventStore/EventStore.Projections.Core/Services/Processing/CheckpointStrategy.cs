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
        private readonly EventFilter _eventFilter;
        private readonly PositionTagger _positionTagger;
        private readonly StatePartitionSelector _statePartitionSelector;

        public class Builder : QuerySourceProcessingStrategyBuilder
        {
            public CheckpointStrategy Build()
            {
                base.Validate();
                return new CheckpointStrategy(
                    _allStreams, ToSet(_categories), ToSet(_streams), _allEvents, ToSet(_events), _byStream);
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

        public StatePartitionSelector StatePartitionSelector
        {
            get { return _statePartitionSelector; }
        }

        public EventDistributionPoint CreatePausedEventDistributionPoint(Guid distributionPointId, IPublisher publisher, CheckpointTag checkpointTag)
        {
            if (_allStreams)
            {
                var distributionPoint = new TransactionFileReaderEventDistributionPoint(
                    publisher, distributionPointId,
                    new EventPosition(checkpointTag.CommitPosition.Value, checkpointTag.PreparePosition.Value));
                return distributionPoint;
            }
            else if (_streams != null && _streams.Count == 1)
            {
                var streamName = checkpointTag.Streams.Keys.First();
                //TODO: handle if not the same
                return CreatePausedStreamReaderEventDistributionPoint(distributionPointId, publisher, checkpointTag, streamName,
                    resolveLinkTos: true, category: null);
            }
            else if (_categories != null && _categories.Count == 1)
            {
                var streamName = checkpointTag.Streams.Keys.First();
                return CreatePausedStreamReaderEventDistributionPoint(distributionPointId, publisher, checkpointTag, streamName,
                    resolveLinkTos: true, category: _categories.First());
            }
            else
                throw new NotSupportedException();
        }

        private static EventDistributionPoint CreatePausedStreamReaderEventDistributionPoint(Guid distributionPointId, IPublisher publisher, CheckpointTag checkpointTag,
            string streamName, bool resolveLinkTos, string category)
        {
            var lastProcessedSequenceNumber = checkpointTag.Streams.Values.First();
            var fromSequenceNumber = lastProcessedSequenceNumber + 1;
            var distributionPoint = new StreamReaderEventDistributionPoint(
                publisher, distributionPointId, streamName, fromSequenceNumber, resolveLinkTos,
                category);
            return distributionPoint;
        }

        public bool IsCheckpointTagAfterEventPosition(CheckpointTag checkpointTag, EventPosition position)
        {
            if (_streams == null || _streams.Count == 0)
            {
                return checkpointTag.Position >= position;
            }
            else if (_streams != null && _streams.Count == 1)
            {
                return checkpointTag.PreparePosition >= position.CommitPosition;
            }
            else if (_categories != null && _categories.Count == 1)
            {
                return checkpointTag.PreparePosition >= position.CommitPosition;
            }
            else
                throw new NotSupportedException();
        }

        private CheckpointStrategy(
            bool allStreams, HashSet<string> categories, HashSet<string> streams, bool allEvents, HashSet<string> events,
            bool byStream)
        {
            _allStreams = allStreams;
            _categories = categories;
            _streams = streams;
            _allEvents = allEvents;
            _events = events;
            _byStream = byStream;

            _eventFilter = CreateEventFilter();
            _positionTagger = CreatePositionTagger();
            _statePartitionSelector = CreateStatePartitionSelector();
        }

        private EventFilter CreateEventFilter()
        {
            if (_allStreams)
                return new TransactionFileEventFilter(_allEvents, _events);
            else if (_categories != null && _categories.Count == 1)
                return new CategoryEventFilter(_categories.First(), _allEvents, _events);
            else if (_categories != null)
                throw new NotSupportedException();
            else if (_streams != null && _streams.Count == 1)
                return new StreamEventFilter(_streams.First(), _allEvents, _events);
            else
                throw new NotSupportedException();
        }

        private PositionTagger CreatePositionTagger()
        {
            if (_allStreams)
                return new TransactionFilePositionTagger();
            else if (_categories != null && _categories.Count == 1)
                //TODO: '-' is a hardcoded separator
                return new StreamPositionTagger("$ce-" + _categories.First());
            else if (_categories != null)
                throw new NotSupportedException();
            else if (_streams != null && _streams.Count == 1)
                return new StreamPositionTagger(_streams.First());
            else
                throw new NotSupportedException();
        }

        private StatePartitionSelector CreateStatePartitionSelector()
        {
            return _byStream
                       ? (StatePartitionSelector) new ByStreamStatePartitionSelector()
                       : new NoopStatePartitionSelector();
        }
    }
}
