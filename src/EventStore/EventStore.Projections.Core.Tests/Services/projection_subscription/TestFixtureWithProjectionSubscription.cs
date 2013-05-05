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
using EventStore.Core.Bus;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_subscription
{
    public abstract class TestFixtureWithProjectionSubscription
    {
        protected Guid _projectionCorrelationId;
        protected TestHandler<EventReaderSubscriptionMessage.CommittedEventReceived> _eventHandler;
        protected TestHandler<EventReaderSubscriptionMessage.CheckpointSuggested> _checkpointHandler;
        protected TestHandler<EventReaderSubscriptionMessage.ProgressChanged> _progressHandler;
        protected TestHandler<EventReaderSubscriptionMessage.EofReached> _eofHandler;
        protected IReaderSubscription _subscription;
        protected IEventReader ForkedReader;
        protected InMemoryBus _bus;
        protected Action<QuerySourceProcessingStrategyBuilder> _source = null;
        protected int _checkpointUnhandledBytesThreshold;
        protected int _checkpointProcessedEventsThreshold;
        protected IReaderStrategy _readerStrategy;

        [SetUp]
        public void setup()
        {
            _checkpointUnhandledBytesThreshold = 1000;
            _checkpointProcessedEventsThreshold = 2000;
            Given();
            _bus = new InMemoryBus("bus");
            _projectionCorrelationId = Guid.NewGuid();
            _eventHandler = new TestHandler<EventReaderSubscriptionMessage.CommittedEventReceived>();
            _checkpointHandler = new TestHandler<EventReaderSubscriptionMessage.CheckpointSuggested>();
            _progressHandler = new TestHandler<EventReaderSubscriptionMessage.ProgressChanged>();
            _eofHandler = new TestHandler<EventReaderSubscriptionMessage.EofReached>();

            _bus.Subscribe(_eventHandler);
            _bus.Subscribe(_checkpointHandler);
            _bus.Subscribe(_progressHandler);
            _bus.Subscribe(_eofHandler);
            _readerStrategy = CreateCheckpointStrategy().ReaderStrategy;
            _subscription = CreateProjectionSubscription();


            When();
        }

        protected virtual IReaderSubscription CreateProjectionSubscription()
        {
            return new ReaderSubscription(
                _bus, _projectionCorrelationId, _readerStrategy.PositionTagger.MakeZeroCheckpointTag(), _readerStrategy,
                _checkpointUnhandledBytesThreshold, _checkpointProcessedEventsThreshold);
        }

        protected virtual void Given()
        {
        }

        protected abstract void When();

        protected virtual CheckpointStrategy CreateCheckpointStrategy()
        {
            var result = new CheckpointStrategy.Builder();
            var readerBuilder = new ReaderStrategy.Builder();
            if (_source != null)
            {
                _source(readerBuilder);
                _source(result);
            }
            else
            {
                readerBuilder.FromAll();
                readerBuilder.AllEvents();
                result.FromAll();
                result.AllEvents();
            }
            var config = ProjectionConfig.GetTest();
            return result.Build(config, readerBuilder.Build());
        }
    }
}
