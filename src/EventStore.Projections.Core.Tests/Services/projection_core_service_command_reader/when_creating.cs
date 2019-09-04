using System;
using EventStore.Projections.Core.Services.Processing;
using EventStore.Projections.Core.Tests.Services.core_projection;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.projection_core_service_command_reader {
	[TestFixture]
	public class when_creating : TestFixtureWithExistingEvents {
		private ProjectionCoreServiceCommandReader _commandReader;
		private Exception _exception;

		[SetUp]
		public new void When() {
			_exception = null;
			try {
				_commandReader = new ProjectionCoreServiceCommandReader(
					_bus,
					_ioDispatcher,
					Guid.NewGuid().ToString("N"));
			} catch (Exception ex) {
				_exception = ex;
			}
		}

		[Test]
		public void does_not_throw() {
			Assert.IsNull(_exception);
		}

		[Test]
		public void it_can_be_created() {
			Assert.IsNotNull(_commandReader);
		}
	}
}
