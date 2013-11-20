﻿// Copyright (c) 2012, Event Store LLP
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
using EventStore.Common.Options;
using EventStore.Core.Bus;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Core.Util;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager
{
    [TestFixture]
    public class when_creating_projection_manager
    {
        private ITimeProvider _timeProvider;

        [SetUp]
        public void setup()
        {
            _timeProvider = new FakeTimeProvider();
        }

        [Test]
        public void it_can_be_created()
        {
            using (
                new ProjectionManager(
                    new FakePublisher(), new FakePublisher(), new IPublisher[] {new FakePublisher()}, _timeProvider,
                    RunProjections.All))
            {
            }
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void main_queue_throws_argument_null_exception()
        {
            using (
                new ProjectionManager(
                    null, new FakePublisher(), new IPublisher[] {new FakePublisher()}, _timeProvider, RunProjections.All))
            {
            }
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_publisher_throws_argument_null_exception()
        {
            using (
                new ProjectionManager(
                    new FakePublisher(), null, new IPublisher[] {new FakePublisher()}, _timeProvider, RunProjections.All))
            {
            }
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_queues_throws_argument_null_exception()
        {
            using (new ProjectionManager(new FakePublisher(), new FakePublisher(), null, _timeProvider, RunProjections.All))
            {
            }
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_queues_throws_argument_exception()
        {
            using (new ProjectionManager(
                    new FakePublisher(), new FakePublisher(), new IPublisher[0], _timeProvider, RunProjections.All))
            {
            }
        }
    }
}
