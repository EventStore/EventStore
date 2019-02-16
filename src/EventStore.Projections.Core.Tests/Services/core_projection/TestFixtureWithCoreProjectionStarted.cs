using System;
using System.Linq;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Management;

namespace EventStore.Projections.Core.Tests.Services.core_projection {
	public abstract class TestFixtureWithCoreProjectionStarted : TestFixtureWithCoreProjection {
		protected Guid _subscriptionId;
		protected SlaveProjectionCommunicationChannels _slaveProjections;

		protected override void Given() {
			base.Given();
			_slaveProjections = null;
		}

		protected override void PreWhen() {
			_coreProjection.Start();
			if (_slaveProjections != null)
				_coreProjection.Handle(
					new ProjectionManagementMessage.SlaveProjectionsStarted(
						_projectionCorrelationId,
						_workerId,
						_slaveProjections));
			var lastSubscribe =
				_consumer.HandledMessages.OfType<ReaderSubscriptionManagement.Subscribe>().LastOrDefault();
			_subscriptionId = lastSubscribe != null ? lastSubscribe.SubscriptionId : Guid.NewGuid();
			_bus.Publish(new EventReaderSubscriptionMessage.ReaderAssignedReader(_subscriptionId, Guid.NewGuid()));
		}
	}
}
