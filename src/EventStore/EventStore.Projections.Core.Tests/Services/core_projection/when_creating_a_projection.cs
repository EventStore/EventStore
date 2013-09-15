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
using EventStore.Core.Helpers;
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
        [SetUp]
        public void Setup()
        {
            var fakePublisher = new FakePublisher();
            _ioDispatcher = new IODispatcher(fakePublisher, new PublishEnvelope(fakePublisher));

            _subscriptionDispatcher =
                new ReaderSubscriptionDispatcher(new FakePublisher());
        }

        private readonly ProjectionConfig _defaultProjectionConfig = new ProjectionConfig(
            null, 5, 10, 1000, 250, true, true, true, true, false);

        private IODispatcher _ioDispatcher;


        private
            ReaderSubscriptionDispatcher
            _subscriptionDispatcher;

        [Test, ExpectedException(typeof (ArgumentException))]
        public void a_checkpoint_threshold_less_tan_checkpoint_handled_threshold_throws_argument_out_of_range_exception(
            )
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            var projectionConfig = new ProjectionConfig(null, 10, 5, 1000, 250, true, true, false, false, false);
            new ContinuousProjectionProcessingStrategy(
                "projection", version, projectionStateHandler, projectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), new FakePublisher(), _ioDispatcher, _subscriptionDispatcher, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentOutOfRangeException))]
        public void a_negative_checkpoint_handled_interval_throws_argument_out_of_range_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            var projectionConfig = new ProjectionConfig(null, -1, 10, 1000, 250, true, true, false, false, false);
            new ContinuousProjectionProcessingStrategy(
                "projection", version, projectionStateHandler, projectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), new FakePublisher(), _ioDispatcher, _subscriptionDispatcher, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_io_dispatcher__throws_argument_null_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            new ContinuousProjectionProcessingStrategy(
                "projection", version, projectionStateHandler, _defaultProjectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), new FakePublisher(), null, _subscriptionDispatcher, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_name_throws_argument_null_excveption()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            new ContinuousProjectionProcessingStrategy(
                null, version, projectionStateHandler, _defaultProjectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), new FakePublisher(), _ioDispatcher, _subscriptionDispatcher, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_publisher_throws_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            new ContinuousProjectionProcessingStrategy(
                "projection", version, projectionStateHandler, _defaultProjectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), null, _ioDispatcher, _subscriptionDispatcher, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_subscription_dispatcher__throws_argument_null_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            new ContinuousProjectionProcessingStrategy(
                "projection", version, projectionStateHandler, _defaultProjectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), new FakePublisher(), _ioDispatcher, null, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void a_null_time_provider__throws_argument_null_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            new ContinuousProjectionProcessingStrategy(
                "projection", version, projectionStateHandler, _defaultProjectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), new FakePublisher(), _ioDispatcher, _subscriptionDispatcher, null);
        }

        [Test, ExpectedException(typeof (ArgumentOutOfRangeException))]
        public void a_zero_checkpoint_handled_threshold_throws_argument_out_of_range_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            var projectionConfig = new ProjectionConfig(null, 0, 10, 1000, 250, true, true, false, false, false);
            new ContinuousProjectionProcessingStrategy(
                "projection", version, projectionStateHandler, projectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), new FakePublisher(), _ioDispatcher, _subscriptionDispatcher, new RealTimeProvider());
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void an_empty_name_throws_argument_exception()
        {
            IProjectionStateHandler projectionStateHandler = new FakeProjectionStateHandler();
            var version = new ProjectionVersion(1, 0, 0);
            new ContinuousProjectionProcessingStrategy(
                "", version, projectionStateHandler, _defaultProjectionConfig,
                projectionStateHandler.GetSourceDefinition(), null, _subscriptionDispatcher).Create(
                    Guid.NewGuid(), new FakePublisher(), _ioDispatcher, _subscriptionDispatcher, new RealTimeProvider());
        }
    }
}
