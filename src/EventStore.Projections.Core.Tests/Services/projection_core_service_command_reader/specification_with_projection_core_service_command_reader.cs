using System;
using System.Collections.Generic;
using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;
using NUnit.Framework;
using TestFixtureWithExistingEvents =
    EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithExistingEvents;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader
{
    public class specification_with_projection_core_service_command_reader : TestFixtureWithExistingEvents
    {
        private ProjectionCoreServiceCommandReader _commandReader;

        protected override void Given()
        {
            base.Given();
            AllWritesSucceed();
            NoOtherStreams();

            _commandReader = new ProjectionCoreServiceCommandReader(_bus, _ioDispatcher, Guid.NewGuid().ToString("N"));

            _bus.Subscribe<ProjectionCoreServiceMessage.StartCore>(_commandReader);
            _bus.Subscribe<ProjectionCoreServiceMessage.StopCore>(_commandReader);
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
