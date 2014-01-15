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
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Services.TimerService;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ParallelQueryAllStreamsMasterReaderStrategy : IReaderStrategy
    {
        private readonly IPrincipal _runAs;
        private readonly ITimeProvider _timeProvider;
        private readonly EventFilter _eventFilter;
        private readonly PositionTagger _positionTagger;

        public ParallelQueryAllStreamsMasterReaderStrategy(
            int phase, IPrincipal runAs, ITimeProvider timeProvider)
        {
            _runAs = runAs;
            _timeProvider = timeProvider;
            _eventFilter = new StreamEventFilter("$streams", true, null);
            _positionTagger = new CatalogStreamPositionTagger(phase, "$streams");
        }

        public bool IsReadingOrderRepeatable
        {
            get { return true; }
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
            return new ReaderSubscription(
                publisher, subscriptionId, fromCheckpointTag, this,
                readerSubscriptionOptions.CheckpointUnhandledBytesThreshold,
                readerSubscriptionOptions.CheckpointProcessedEventsThreshold, readerSubscriptionOptions.StopOnEof,
                readerSubscriptionOptions.StopAfterNEvents);
        }

        public IEventReader CreatePausedEventReader(
            Guid eventReaderId, IPublisher publisher, IODispatcher ioDispatcher, CheckpointTag checkpointTag,
            bool stopOnEof, int? stopAfterNEvents)
        {
            return new AllStreamsCatalogEventReader(
                ioDispatcher, publisher, eventReaderId, _runAs, checkpointTag.CatalogPosition + 1, _timeProvider,
                resolveLinkTos: true, stopOnEof: stopOnEof, stopAfterNEvents: stopAfterNEvents);
        }
    }
}
