using System;
using System.Collections.Generic;
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
            var queues = new Dictionary<Guid, IPublisher> {{Guid.NewGuid(), new FakePublisher()}};
            using (
                new ProjectionManager(
                    new FakePublisher(), new FakePublisher(), queues, _timeProvider,
                    RunProjections.All, ProjectionManagerNode.CreateTimeoutSchedulers(queues.Count)))
            {
            }
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void main_queue_throws_argument_null_exception()
        {
            var queues = new Dictionary<Guid, IPublisher> {{Guid.NewGuid(), new FakePublisher()}};
            using (
                new ProjectionManager(
                    null, new FakePublisher(), queues, _timeProvider, RunProjections.All, ProjectionManagerNode.CreateTimeoutSchedulers(queues.Count)))
            {
            }
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_publisher_throws_argument_null_exception()
        {
            var queues = new Dictionary<Guid, IPublisher> {{Guid.NewGuid(), new FakePublisher()}};
            using (
                new ProjectionManager(
                    new FakePublisher(), null, queues, _timeProvider, RunProjections.All, ProjectionManagerNode.CreateTimeoutSchedulers(queues.Count)))
            {
            }
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_queues_throws_argument_null_exception()
        {
            using (
                new ProjectionManager(
                    new FakePublisher(),
                    new FakePublisher(),
                    null,
                    _timeProvider,
                    RunProjections.All,
                    ProjectionManagerNode.CreateTimeoutSchedulers(1)))
            {
            }
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_queues_throws_argument_exception()
        {
            var queues = new Dictionary<Guid, IPublisher>();
            using (
                new ProjectionManager(
                    new FakePublisher(),
                    new FakePublisher(),
                    queues,
                    _timeProvider,
                    RunProjections.All,
                    ProjectionManagerNode.CreateTimeoutSchedulers(queues.Count)))
            {
            }
        }
    }
}
