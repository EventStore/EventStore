using System.Linq;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	[TestFixture]
	public class when_loading_a_new_projection : TestFixtureWithCoreProjectionLoaded {
		protected override void Given() {
			NoStream("$projections-projection-result");
			NoStream("$projections-projection-order");
			AllWritesToSucceed("$projections-projection-order");
			NoStream("$projections-projection-checkpoint");
		}

		protected override void When() {
		}

		[Test]
		public void should_not_subscribe() {
			Assert.AreEqual(0, _subscribeProjectionHandler.HandledMessages.Count);
		}

		[Test]
		public void should_not_initialize_projection_state_handler() {
			Assert.AreEqual(0, _stateHandler._initializeCalled);
		}

		[Test]
		public void should_not_publish_started_message() {
			Assert.AreEqual(0, _consumer.HandledMessages.OfType<CoreProjectionStatusMessage.Started>().Count());
		}
	}
}
