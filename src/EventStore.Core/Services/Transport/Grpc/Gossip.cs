using System;
using EventStore.Core.Bus;
using EventStore.Core.Metrics;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Gossip : EventStore.Client.Gossip.Gossip.GossipBase  {
		private readonly IPublisher _queue;
		private readonly IAuthorizationProvider _authorizationProvider;
		private readonly IDurationTracker _durationTracker;
		private readonly GossipTracker _gossipTracker;

		public Gossip(IPublisher queue, ISubscriber bus, IAuthorizationProvider authorizationProvider,
			IDurationTracker durationTracker) {
			_queue = queue;
			_authorizationProvider =
				authorizationProvider ?? throw new ArgumentNullException(nameof(authorizationProvider));
			_durationTracker = durationTracker;
			_gossipTracker = new GossipTracker(bus);
		}
	}
}
