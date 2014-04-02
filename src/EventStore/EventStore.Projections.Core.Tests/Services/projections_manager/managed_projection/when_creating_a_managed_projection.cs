using System;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Services.TimeService;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;
using TestFixtureWithReadWriteDispatchers = EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithReadWriteDispatchers;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.managed_projection
{
    [TestFixture]
    public class when_creating_a_managed_projection : TestFixtureWithReadWriteDispatchers
    {
        private new ITimeProvider _timeProvider;

        [SetUp]
        public void setup()
        {
            _timeProvider = new FakeTimeProvider();
        }


        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_guid_throws_invali_argument_exception()
        {
            new ManagedProjection(
                Guid.NewGuid(),
                Guid.Empty,
                1,
                "name",
                true,
                null,
                _writeDispatcher,
                _readDispatcher,
                _bus,
                _bus,
                _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_guid_throws_invali_argument_exception2()
        {
            new ManagedProjection(
                Guid.NewGuid(),
                Guid.Empty,
                1,
                "name",
                true,
                null,
                _writeDispatcher,
                _readDispatcher,
                _bus,
                _bus,
                _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_name_throws_argument_null_exception()
        {
            new ManagedProjection(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                null,
                true,
                null,
                _writeDispatcher,
                _readDispatcher,
                _bus,
                _bus,
                _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentNullException))]
        public void null_name_throws_argument_null_exception2()
        {
            new ManagedProjection(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                null,
                true,
                null,
                _writeDispatcher,
                _readDispatcher,
                _bus,
                _bus,
                _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_name_throws_argument_exception()
        {
            new ManagedProjection(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "",
                true,
                null,
                _writeDispatcher,
                _readDispatcher,
                _bus,
                _bus,
                _timeProvider);
        }

        [Test, ExpectedException(typeof (ArgumentException))]
        public void empty_name_throws_argument_exception2()
        {
            new ManagedProjection(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                "",
                true,
                null,
                _writeDispatcher,
                _readDispatcher,
                _bus,
                _bus,
                _timeProvider);
        }
    }
}
