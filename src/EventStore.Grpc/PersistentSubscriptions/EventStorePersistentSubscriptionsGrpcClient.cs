using System;
using Grpc.Core;

namespace EventStore.Grpc.PersistentSubscriptions {
	public partial class EventStorePersistentSubscriptionsGrpcClient {
		private readonly PersistentSubscriptions.PersistentSubscriptionsClient _client;

		public EventStorePersistentSubscriptionsGrpcClient(CallInvoker callInvoker) {
			if (callInvoker == null) {
				throw new ArgumentNullException(nameof(callInvoker));
			}

			_client = new PersistentSubscriptions.PersistentSubscriptionsClient(callInvoker);
		}
	}
}
