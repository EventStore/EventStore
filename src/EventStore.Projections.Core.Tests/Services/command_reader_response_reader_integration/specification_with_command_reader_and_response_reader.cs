using System;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using TestFixtureWithExistingEvents =
    EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithExistingEvents;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Tests.Services.command_reader_response_reader_integration
{
    public class specification_with_command_reader_and_response_reader : TestFixtureWithExistingEvents
    {
        protected ProjectionCoreServiceCommandReader _commandReader;
        protected ProjectionManagerResponseReader _responseReader;
        protected int _numberOfWorkers;

        protected override void Given()
        {
            base.Given();
            AllWritesSucceed();
            NoOtherStreams();

            _commandReader = new ProjectionCoreServiceCommandReader(_bus, _ioDispatcher, Guid.NewGuid().ToString("N"));
            _responseReader = new ProjectionManagerResponseReader(_bus, _ioDispatcher, _numberOfWorkers);

            _bus.Subscribe<ProjectionCoreServiceMessage.StartCore>(_commandReader);
            _bus.Subscribe<ProjectionCoreServiceMessage.StopCore>(_commandReader);

            _bus.Subscribe<ProjectionManagementMessage.Starting>(_responseReader);
        }

        [SetUp]
        public new void SetUp()
        {
            WhenLoop();
        }

        protected override ManualQueue GiveInputQueue()
        {
            return new ManualQueue(_bus, _timeProvider);
        }
    }
}