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
using EventStore.Core.Bus;
using EventStore.Core.Services.TimerService;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ReaderStrategy : IReaderStrategy
    {
        private readonly bool _allStreams;
        private readonly HashSet<string> _categories;
        private readonly HashSet<string> _streams;
        private readonly bool _allEvents;
        private readonly bool _includeLinks;
        private readonly HashSet<string> _events;
        private readonly bool _useEventIndexes;
        private readonly bool _reorderEvents;
        private readonly int _processingLag;


        private readonly EventFilter _eventFilter;
        private readonly PositionTagger _positionTagger;


        public class Builder : QuerySourceProcessingStrategyBuilder
        {
            public IReaderStrategy Build()
            {
                base.Validate();
                HashSet<string> categories = ToSet(_categories);
                HashSet<string> streams = ToSet(_streams);
                bool includeLinks = _options.IncludeLinks;
                HashSet<string> events = ToSet(_events);
                bool useEventIndexes = _options.UseEventIndexes;
                bool reorderEvents = _options.ReorderEvents;
                int processingLag = _options.ProcessingLag;
                var readerStrategy = new ReaderStrategy(
                    _allStreams, categories, streams, _allEvents, includeLinks, events, processingLag, reorderEvents,
                    useEventIndexes);
                return readerStrategy;
            }
        }

        public static IReaderStrategy Create(ISourceDefinitionConfigurator sources)
        {
            var builder = new Builder();
            sources.ConfigureSourceProcessingStrategy(builder);
            return builder.Build();
        }

        private ReaderStrategy(
            bool allStreams, HashSet<string> categories, HashSet<string> streams, bool allEvents, bool includeLinks,
            HashSet<string> events, int processingLag, bool reorderEvents, bool useEventIndexes)
        {
            _allStreams = allStreams;
            _categories = categories;
            _streams = streams;
            _allEvents = allEvents;
            _includeLinks = includeLinks;
            _events = events;
            _processingLag = processingLag;
            _reorderEvents = reorderEvents;
            _useEventIndexes = useEventIndexes;

            _eventFilter = CreateEventFilter();
            _positionTagger = CreatePositionTagger();
        }

        public bool IsReadingOrderRepeatable {
            get
            {
                return !((_allStreams && _useEventIndexes && _events != null && _events.Count > 1)
                       || (_streams != null && _streams.Count > 1));
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

        public IReaderSubscription CreateReaderSubscription(
            IPublisher publisher, CheckpointTag fromCheckpointTag, Guid subscriptionId,
            ReaderSubscriptionOptions readerSubscriptionOptions)
        {
            if (_reorderEvents)
                return new EventReorderingReaderSubscription(
                    publisher, subscriptionId, fromCheckpointTag, this,
                    readerSubscriptionOptions.CheckpointUnhandledBytesThreshold,
                    readerSubscriptionOptions.CheckpointProcessedEventsThreshold, _processingLag,
                    readerSubscriptionOptions.StopOnEof, readerSubscriptionOptions.StopAfterNEvents);
            else
                return new ReaderSubscription(
                    publisher, subscriptionId, fromCheckpointTag, this,
                    readerSubscriptionOptions.CheckpointUnhandledBytesThreshold,
                    readerSubscriptionOptions.CheckpointProcessedEventsThreshold, readerSubscriptionOptions.StopOnEof,
                    readerSubscriptionOptions.StopAfterNEvents);
        }

        public IEventReader CreatePausedEventReader(
            Guid eventReaderId, IPublisher publisher, CheckpointTag checkpointTag, bool stopOnEof, int? stopAfterNEvents)
        {
            if (_allStreams && _useEventIndexes && _events != null && _events.Count == 1)
            {
                var streamName = checkpointTag.Streams.Keys.First();
                return CreatePausedStreamEventReader(
                    eventReaderId, publisher, checkpointTag, streamName, stopOnEof, resolveLinkTos: true,
                    stopAfterNEvents: stopAfterNEvents);
            }
            if (_allStreams && _useEventIndexes && _events != null && _events.Count > 1)
            {
                IEnumerable<string> streams = GetEventIndexStreams();
                return CreatePausedEventIndexEventReader(
                    eventReaderId, publisher, checkpointTag, stopOnEof, stopAfterNEvents, true, streams);
            }
            if (_allStreams)
            {
                var eventReader = new TransactionFileEventReader(
                    publisher, eventReaderId,
                    new TFPos(checkpointTag.CommitPosition.Value, checkpointTag.PreparePosition.Value),
                    new RealTimeProvider(), deliverEndOfTFPosition: true, stopOnEof: stopOnEof, resolveLinkTos: false,
                    stopAfterNEvents: stopAfterNEvents);
                return eventReader;
            }
            if (_streams != null && _streams.Count == 1)
            {
                var streamName = checkpointTag.Streams.Keys.First();
                //TODO: handle if not the same
                return CreatePausedStreamEventReader(
                    eventReaderId, publisher, checkpointTag, streamName, stopOnEof, resolveLinkTos: true,
                    stopAfterNEvents: stopAfterNEvents);
            }
            if (_categories != null && _categories.Count == 1)
            {
                var streamName = checkpointTag.Streams.Keys.First();
                return CreatePausedStreamEventReader(
                    eventReaderId, publisher, checkpointTag, streamName, stopOnEof, resolveLinkTos: true,
                    stopAfterNEvents: stopAfterNEvents);
            }
            if (_streams != null && _streams.Count > 1)
            {
                return CreatePausedMultiStreamEventReader(
                    eventReaderId, publisher, checkpointTag, stopOnEof, stopAfterNEvents, true, _streams);
            }
            throw new NotSupportedException();
        }

        private EventFilter CreateEventFilter()
        {
            if (_allStreams && _useEventIndexes && _events != null && _events.Count == 1)
                return new IndexedEventTypeEventFilter(_events.First());
            if (_allStreams && _useEventIndexes && _events != null && _events.Count > 1)
                return new IndexedEventTypesEventFilter(_events.ToArray());
            if (_allStreams)
                return new TransactionFileEventFilter(_allEvents, _events, includeLinks: _includeLinks);
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

        private static IEventReader CreatePausedStreamEventReader(
            Guid eventReaderId, IPublisher publisher, CheckpointTag checkpointTag, string streamName, bool stopOnEof,
            int? stopAfterNEvents, bool resolveLinkTos)
        {
            var lastProcessedSequenceNumber = checkpointTag.Streams.Values.First();
            var fromSequenceNumber = lastProcessedSequenceNumber + 1;
            var eventReader = new StreamEventReader(
                publisher, eventReaderId, streamName, fromSequenceNumber, new RealTimeProvider(), resolveLinkTos,
                stopOnEof, stopAfterNEvents);
            return eventReader;
        }

        private static IEventReader CreatePausedEventIndexEventReader(
            Guid eventReaderId, IPublisher publisher, CheckpointTag checkpointTag, bool stopOnEof, int? stopAfterNEvents,
            bool resolveLinkTos, IEnumerable<string> streams)
        {
            var nextPositions = checkpointTag.Streams.ToDictionary(v => v.Key, v => v.Value + 1);

            return new EventIndexEventReader(
                publisher, eventReaderId, streams.ToArray(), nextPositions, resolveLinkTos, new RealTimeProvider(),
                stopOnEof, stopAfterNEvents);
        }

        private static IEventReader CreatePausedMultiStreamEventReader(
            Guid eventReaderId, IPublisher publisher, CheckpointTag checkpointTag, bool stopOnEof, int? stopAfterNEvents,
            bool resolveLinkTos, IEnumerable<string> streams)
        {
            var nextPositions = checkpointTag.Streams.ToDictionary(v => v.Key, v => v.Value + 1);

            return new MultiStreamEventReader(
                publisher, eventReaderId, streams.ToArray(), nextPositions, resolveLinkTos, new RealTimeProvider(),
                stopOnEof, stopAfterNEvents);
        }
    }
}
