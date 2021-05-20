using System;
using EventStore.Core.Bus;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Gossip : EventStore.Client.Gossip.Gossip.GossipBase {
		private readonly IPublisher _bus;
		private readonly IAuthorizationProvider _authorizationProvider;
		public Gossip(IPublisher bus, IAuthorizationProvider authorizationProvider) {
			_bus = bus;
			_authorizationProvider =
				authorizationProvider ?? throw new ArgumentNullException(nameof(authorizationProvider));
		}
	}
}
