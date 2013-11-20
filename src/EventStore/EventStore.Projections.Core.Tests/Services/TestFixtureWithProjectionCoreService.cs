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
using EventStore.Core.Data;
using EventStore.Core.Helpers;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Bus.Helpers;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Messages.ParallelQueryProcessingMessages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using ResolvedEvent = EventStore.Projections.Core.Services.Processing.ResolvedEvent;

namespace EventStore.Projections.Core.Tests.Services
{
    public class TestFixtureWithProjectionCoreService
    {
        public class TestCoreProjection : ICoreProjection
        {
            public List<EventReaderSubscriptionMessage.CommittedEventReceived> HandledMessages =
                new List<EventReaderSubscriptionMessage.CommittedEventReceived>();

            public void Handle(CoreProjectionProcessingMessage.CheckpointCompleted message)
            {
                throw new NotImplementedException();
            }

            public void Handle(CoreProjectionProcessingMessage.CheckpointLoaded message)
            {
                throw new NotImplementedException();
            }


            public void Handle(CoreProjectionProcessingMessage.RestartRequested message)
            {
                throw new NotImplementedException();
            }

            public void Handle(CoreProjectionProcessingMessage.Failed message)
            {
                throw new NotImplementedException();
            }

            public void Handle(CoreProjectionProcessingMessage.PrerecordedEventsLoaded message)
            {
                throw new NotImplementedException();
            }
        }

        protected TestHandler<Message> _consumer;
        protected InMemoryBus _bus;
        protected ProjectionCoreService _service;
        protected EventReaderCoreService _readerService;

        private
            ReaderSubscriptionDispatcher
            _subscriptionDispatcher;

        private SpooledStreamReadingDispatcher _spoolProcessingResponseDispatcher;

        [SetUp]
        public void Setup()
        {
            _consumer = new TestHandler<Message>();
            _bus = new InMemoryBus("temp");
            _bus.Subscribe(_consumer);
            ICheckpoint writerCheckpoint = new InMemoryCheckpoint(1000);
            var ioDispatcher = new IODispatcher(_bus, new PublishEnvelope(_bus));
            _readerService = new EventReaderCoreService(_bus, ioDispatcher, 10, writerCheckpoint, runHeadingReader: true);
            _subscriptionDispatcher =
                new ReaderSubscriptionDispatcher(_bus);
            _spoolProcessingResponseDispatcher = new SpooledStreamReadingDispatcher(_bus);
            _service = new ProjectionCoreService(
                _bus, _bus, _subscriptionDispatcher, new RealTimeProvider(), ioDispatcher,
                _spoolProcessingResponseDispatcher);
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
            _bus.Subscribe(_spoolProcessingResponseDispatcher.CreateSubscriber<PartitionProcessingResult>());
            _readerService.Handle(new Messages.ReaderCoreServiceMessage.StartReader());
            _service.Handle(new ProjectionCoreServiceMessage.StartCore());
        }

        protected IReaderStrategy CreateReaderStrategy()
        {
            var result = new SourceDefinitionBuilder();
            result.FromAll();
            result.AllEvents();
            return ReaderStrategy.Create(0, result.Build(), new RealTimeProvider(), stopOnEof: false, runAs: null);
        }

        protected static ResolvedEvent CreateEvent()
        {
            return new ResolvedEvent(
                "test", -1, "test", -1, false, new TFPos(10, 5), new TFPos(10, 5), Guid.NewGuid(), "t", false,
                new byte[0], new byte[0], null, default(DateTime));
        }
    }
}
