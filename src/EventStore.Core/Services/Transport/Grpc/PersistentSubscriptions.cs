using System;
using EventStore.Core.Authentication;
using EventStore.Core.Authorization;
using EventStore.Core.Bus;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class PersistentSubscriptions
		: EventStore.Client.PersistentSubscriptions.PersistentSubscriptions.PersistentSubscriptionsBase {
		private readonly IPublisher _publisher;
		private readonly IAuthorizationProvider _authorizationProvider;

		public PersistentSubscriptions(IPublisher publisher, IAuthorizationProvider authorizationProvider) {
			if (publisher == null) throw new ArgumentNullException(nameof(publisher));
			if (authorizationProvider == null) throw new ArgumentNullException(nameof(authorizationProvider));
			_publisher = publisher;
			_authorizationProvider = authorizationProvider;
		}
	}
}
