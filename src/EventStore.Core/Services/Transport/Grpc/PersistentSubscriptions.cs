using System;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class PersistentSubscriptions
		: EventStore.Grpc.PersistentSubscriptions.PersistentSubscriptions.PersistentSubscriptionsBase {
		private readonly IQueuedHandler _queue;
		private readonly IAuthenticationProvider _authenticationProvider;

		public PersistentSubscriptions(IQueuedHandler queue, IAuthenticationProvider authenticationProvider) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));
			if (authenticationProvider == null) throw new ArgumentNullException(nameof(authenticationProvider));
			_queue = queue;
			_authenticationProvider = authenticationProvider;
		}
	}
}
