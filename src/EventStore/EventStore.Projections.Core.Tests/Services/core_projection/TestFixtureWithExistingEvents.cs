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
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{

    public abstract class TestFixtureWithExistingEvents : EventStore.Core.Tests.Helpers.TestFixtureWithExistingEvents,
                                                          IHandle<ProjectionCoreServiceMessage.CoreTick>,
                                                          IHandle<ReaderCoreServiceMessage.ReaderTick>

    {
        protected
            ReaderSubscriptionDispatcher
            _subscriptionDispatcher;

        protected readonly ProjectionStateHandlerFactory _handlerFactory = new ProjectionStateHandlerFactory();
        private bool _ticksAreHandledImmediately;

        protected override void Given1()
        {
            base.Given1();
            _ticksAreHandledImmediately = false;
        }

        [SetUp]
        public void SetUp()
        {
            _subscriptionDispatcher =
                new ReaderSubscriptionDispatcher
                    (_bus);
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CommittedEventReceived>());
            _bus.Subscribe(
                _subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.CheckpointSuggested>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.EofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionEofReached>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.PartitionMeasured>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ProgressChanged>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.SubscriptionStarted>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.NotAuthorized>());
            _bus.Subscribe(_subscriptionDispatcher.CreateSubscriber<EventReaderSubscriptionMessage.ReaderAssignedReader>());
            _bus.Subscribe<ProjectionCoreServiceMessage.CoreTick>(this);
            _bus.Subscribe<ReaderCoreServiceMessage.ReaderTick>(this);
        }

        public void Handle(ProjectionCoreServiceMessage.CoreTick message)
        {
            if (_ticksAreHandledImmediately)
                message.Action();
        }

        public void Handle(ReaderCoreServiceMessage.ReaderTick message)
        {
            if (_ticksAreHandledImmediately)
                message.Action();
        }

        protected void TicksAreHandledImmediately()
        {
            _ticksAreHandledImmediately = true;
        }

        protected ClientMessage.WriteEvents CreateWriteEvent(
            string streamId, string eventType, string data, string metadata = null, bool isJson = true,
            Guid? correlationId = null)
        {
            return new ClientMessage.WriteEvents(
                Guid.NewGuid(), correlationId ?? Guid.NewGuid(), new PublishEnvelope(GetInputQueue()), false, streamId,
                ExpectedVersion.Any, new Event(Guid.NewGuid(), eventType, isJson, data, metadata), null);
        }
    }
}
