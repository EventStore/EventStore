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
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection
{
    [TestFixture]
    public class when_creating_a_projection
    {
        private readonly ProjectionConfig _defaultProjectionConfig = new ProjectionConfig(null, 
            5, 10, 1000, 250, true, true, true, true);

        private
            RequestResponseDispatcher
                <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>
            _readDispatcher;

        private RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>
            _writeDispatcher;

        private
            PublishSubscribeDispatcher
                <ReaderSubscriptionManagement.Subscribe,
                    ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>
            _subscriptionDispatcher;

        [SetUp]
        public void Setup()
        {
            _readDispatcher =
                new RequestResponseDispatcher
                    <ClientMessage.ReadStreamEventsBackward, ClientMessage.ReadStreamEventsBackwardCompleted>(
                    new FakePublisher(), v => v.CorrelationId, v => v.CorrelationId,
                    new PublishEnvelope(new FakePublisher()));
            _writeDispatcher =
                new RequestResponseDispatcher<ClientMessage.WriteEvents, ClientMessage.WriteEventsCompleted>(
                    new FakePublisher(), v => v.CorrelationId, v => v.CorrelationId,
                    new PublishEnvelope(new FakePublisher()));

            _subscriptionDispatcher =
                new PublishSubscribeDispatcher
                    <ReaderSubscriptionManagement.Subscribe,
                        ReaderSubscriptionManagement.ReaderSubscriptionManagementMessage, EventReaderSubscriptionMessage>
                    (new FakePublisher(), v => v.SubscriptionId, v => v.SubscriptionId);

        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_name_throws_argument_null_excveption()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare(null, new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler, _defaultProjectionConfig,
                                         _readDispatcher, _writeDispatcher, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void an_empty_name_throws_argument_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler, _defaultProjectionConfig,
                                         _readDispatcher, _writeDispatcher, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_publisher_throws_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), null, projectionStateHandler, _defaultProjectionConfig,
                                         _readDispatcher, _writeDispatcher, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_projection_handler_throws_argument_null_exception()
        {
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), null, _defaultProjectionConfig, _readDispatcher,
                                         _writeDispatcher, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentOutOfRangeException))]
        public void a_negative_checkpoint_handled_interval_throws_argument_out_of_range_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler,
                                         new ProjectionConfig(null, -1, 10, 1000, 250, true, true, false, false), _readDispatcher,
                                         _writeDispatcher, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentOutOfRangeException))]
        public void a_zero_checkpoint_handled_threshold_throws_argument_out_of_range_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler,
                                         new ProjectionConfig(null, 0, 10, 1000, 250, true, true, false, false), _readDispatcher,
                                         _writeDispatcher, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void a_checkpoint_threshold_less_tan_checkpoint_handled_threshold_throws_argument_out_of_range_exception(
            )
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler,
                                         new ProjectionConfig(null, 10, 5, 1000, 250, true, true, false, false), _readDispatcher,
                                         _writeDispatcher, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_read_dispatcher__throws_argument_null_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler,
                                         _defaultProjectionConfig, null, _writeDispatcher, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_write_dispatcher__throws_argument_null_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler,
                                         _defaultProjectionConfig, _readDispatcher, null, _subscriptionDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void a_null_subscription_dispatcher__throws_argument_null_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler,
                                         _defaultProjectionConfig, _readDispatcher, _writeDispatcher, null, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof(ArgumentNullException))]
        public void a_null_time_provider__throws_argument_null_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            CoreProjection.CreateAndPrepare("projection", new ProjectionVersion(1, 0, 0), Guid.NewGuid(), new FakePublisher(), projectionStateHandler,
                                         _defaultProjectionConfig, _readDispatcher, _writeDispatcher, _subscriptionDispatcher, null, null);
        }

    }
}
