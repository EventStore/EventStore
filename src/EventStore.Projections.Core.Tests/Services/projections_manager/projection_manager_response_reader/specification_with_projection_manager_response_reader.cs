using EventStore.Core.Tests.Helpers;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;
using NUnit.Framework;
using TestFixtureWithExistingEvents =
	EventStore.Projections.Core.Tests.Services.core_projection.TestFixtureWithExistingEvents;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader {
	public class specification_with_projection_manager_response_reader : TestFixtureWithExistingEvents {
		protected ProjectionManagerResponseReader _commandReader;

		protected override void Given() {
			base.Given();
			AllWritesSucceed();
			NoOtherStreams();

			_commandReader = new ProjectionManagerResponseReader(_bus, _ioDispatcher, 0);

			_bus.Subscribe<ProjectionManagementMessage.Starting>(_commandReader);
		}

		[SetUp]
		public new void SetUp() {
			WhenLoop();
		}

		protected override ManualQueue GiveInputQueue() {
			return new ManualQueue(_bus, _timeProvider);
		}
	}
}
