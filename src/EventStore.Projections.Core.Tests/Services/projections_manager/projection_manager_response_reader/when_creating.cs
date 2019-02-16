using System;
using EventStore.Projections.Core.Services.Management;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projections_manager.projection_manager_response_reader {
	[TestFixture]
	public class when_creating : TestFixtureWithExistingEvents {
		private ProjectionManagerResponseReader _commandReader;

		[SetUp]
		public new void When() {
			_commandReader = new ProjectionManagerResponseReader(_bus, _ioDispatcher, 0);
		}

		[Test]
		public void it_can_be_created() {
			Assert.IsNotNull(_commandReader);
		}
	}
}
