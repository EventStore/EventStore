using System;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class PersistentSubscriptions
		: EventStore.Client.PersistentSubscriptions.PersistentSubscriptions.PersistentSubscriptionsBase {
		private readonly IQueuedHandler _queue;

		public PersistentSubscriptions(IQueuedHandler queue) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));
			_queue = queue;
		}
	}
}
