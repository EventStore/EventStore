using System;

namespace EventStore.Projections.Core.Messages {
	public sealed class SlaveProjectionCommunicationChannel {
		private readonly Guid _subscriptionId;
		private readonly string _managedProjectionName;
		private readonly Guid _workerId;

		public SlaveProjectionCommunicationChannel(
			string managedProjectionName,
			Guid workerId,
			Guid subscriptionId) {
			_managedProjectionName = managedProjectionName;
			_workerId = workerId;
			_subscriptionId = subscriptionId;
		}

		public Guid SubscriptionId {
			get { return _subscriptionId; }
		}

		public string ManagedProjectionName {
			get { return _managedProjectionName; }
		}

		public Guid WorkerId {
			get { return _workerId; }
		}
	}
}
