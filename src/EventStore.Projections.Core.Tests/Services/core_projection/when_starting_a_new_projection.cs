using System;
using EventStore.Projections.Core.Messages;
using NUnit.Framework;
using System.Linq;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	[TestFixture]
	public class when_starting_a_new_projection : TestFixtureWithCoreProjectionStarted {
		protected override void Given() {
			NoStream("$projections-projection-result");
			NoStream("$projections-projection-order");
			AllWritesToSucceed("$projections-projection-order");
			NoStream("$projections-projection-checkpoint");
		}

		protected override void When() {
		}

		[Test]
		public void should_subscribe_from_beginning() {
			Assert.AreEqual(1, _subscribeProjectionHandler.HandledMessages.Count);
			Assert.AreEqual(0, _subscribeProjectionHandler.HandledMessages[0].FromPosition.Position.CommitPosition);
			Assert.AreEqual(-1, _subscribeProjectionHandler.HandledMessages[0].FromPosition.Position.PreparePosition);
		}

		[Test]
		public void should_publish_started_message() {
			Assert.AreEqual(1, _consumer.HandledMessages.OfType<CoreProjectionStatusMessage.Started>().Count());
			var startedMessage = _consumer.HandledMessages.OfType<CoreProjectionStatusMessage.Started>().Single();
			Assert.AreEqual(_projectionCorrelationId, startedMessage.ProjectionId);
		}
	}
}
