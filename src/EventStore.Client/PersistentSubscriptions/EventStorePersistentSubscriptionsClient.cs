using System;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EventStore.Client.PersistentSubscriptions {
	public partial class EventStorePersistentSubscriptionsClient {
		private readonly PersistentSubscriptions.PersistentSubscriptionsClient _client;
		private readonly ILogger _log;

		public EventStorePersistentSubscriptionsClient(CallInvoker callInvoker, EventStoreClientSettings settings) {
			if (callInvoker == null) {
				throw new ArgumentNullException(nameof(callInvoker));
			}

			_client = new PersistentSubscriptions.PersistentSubscriptionsClient(callInvoker);
			_log = settings.LoggerFactory?.CreateLogger<EventStorePersistentSubscriptionsClient>()
			       ?? new NullLogger<EventStorePersistentSubscriptionsClient>();
		}
	}
}
