using System;
using System.Net;

namespace EventStore.Client {
	public class EventStoreClientConnectivitySettings {
		public int MaxDiscoverAttempts { get; set; }
		public EndPoint[] GossipSeeds { get; set; } = Array.Empty<EndPoint>();
		public TimeSpan GossipTimeout { get; set; }
		public TimeSpan DiscoveryInterval { get; set; }
		public NodePreference NodePreference { get; set; }

		public static EventStoreClientConnectivitySettings Default => new EventStoreClientConnectivitySettings {
			MaxDiscoverAttempts = 10,
			GossipTimeout = TimeSpan.FromSeconds(5),
			DiscoveryInterval = TimeSpan.FromMilliseconds(100),
			NodePreference = NodePreference.Leader,
		};
	}
}
