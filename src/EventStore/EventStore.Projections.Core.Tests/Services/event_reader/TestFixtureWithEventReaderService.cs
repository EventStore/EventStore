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
using System.Linq;
using EventStore.Core.Bus;
using EventStore.Core.Tests.Helpers;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using TestFixtureWithExistingEvents = EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithExistingEvents;

namespace EventStore.Projections.Core.Tests.Services.event_reader
{
    public class TestFixtureWithEventReaderService : TestFixtureWithExistingEvents
    {
        protected EventReaderCoreService _readerService;

        protected override void Given1()
        {
            base.Given1();
            EnableReadAll();
        }

        protected override ManualQueue GiveInputQueue()
        {
            return new ManualQueue(_bus);
        }

        [SetUp]
        public void Setup()
        {
            _bus.Subscribe(_consumer);

            ICheckpoint writerCheckpoint = new InMemoryCheckpoint(1000);
            _readerService = new EventReaderCoreService(
                GetInputQueue(), _ioDispatcher, 10, writerCheckpoint, runHeadingReader: GivenHeadingReaderRunning());
            _subscriptionDispatcher =
                new ReaderSubscriptionDispatcher(GetInputQueue());


            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());


            _bus.Subscribe<ReaderCoreServiceMessage.StartReader>(_readerService);
            _bus.Subscribe<ReaderCoreServiceMessage.StopReader>(_readerService);
            _bus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.CommittedEventDistributed>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderEof>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderPartitionEof>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderNotAuthorized>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderIdle>(_readerService);
            _bus.Subscribe<ReaderSubscriptionMessage.EventReaderStarting>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Pause>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Resume>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Subscribe>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.Unsubscribe>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.SpoolStreamReading>(_readerService);
            _bus.Subscribe<ReaderSubscriptionManagement.CompleteSpooledStreamReading>(_readerService);


            GivenAdditionalServices();


            _bus.Publish(new ReaderCoreServiceMessage.StartReader());

            WhenLoop();
        }

        protected virtual bool GivenHeadingReaderRunning()
        {
            return false;
        }

        protected virtual void GivenAdditionalServices()
        {
        }

        protected Guid GetReaderId()
        {
            var readerAssignedMessage =
                _consumer.HandledMessages.OfType<EventReaderSubscriptionMessage.ReaderAssignedReader>().LastOrDefault();
            Assert.IsNotNull(readerAssignedMessage);
            var reader = readerAssignedMessage.ReaderId;
            return reader;
        }
    }
}
