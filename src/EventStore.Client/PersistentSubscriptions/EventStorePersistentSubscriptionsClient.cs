using System;
using Grpc.Core;

namespace EventStore.Client.PersistentSubscriptions {
	public partial class EventStorePersistentSubscriptionsClient {
		private readonly PersistentSubscriptions.PersistentSubscriptionsClient _client;

		public EventStorePersistentSubscriptionsClient(CallInvoker callInvoker) {
			if (callInvoker == null) {
				throw new ArgumentNullException(nameof(callInvoker));
			}

			_client = new PersistentSubscriptions.PersistentSubscriptionsClient(callInvoker);
		}
	}
}
