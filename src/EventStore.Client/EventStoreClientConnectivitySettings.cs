using System;
using System.Net;

namespace EventStore.Client {
	public class EventStoreClientConnectivitySettings {
		public Uri Address { get; set; } = new UriBuilder {
			Scheme = Uri.UriSchemeHttps,
			Port = 2113
		}.Uri;
		public int MaxDiscoverAttempts { get; set; }

		public EndPoint[] GossipSeeds {
			get {
				object endpoints = (object)DnsGossipSeeds ?? IpGossipSeeds;
				return endpoints switch {
					DnsEndPoint[] dns => Array.ConvertAll<DnsEndPoint, EndPoint>(dns, x => x),
					IPEndPoint[] ip => Array.ConvertAll<IPEndPoint, EndPoint>(ip, x => x),
					_ => Array.Empty<EndPoint>()
				};
			}
		}

		public DnsEndPoint[] DnsGossipSeeds { get; set; }
		public IPEndPoint[] IpGossipSeeds { get; set; }
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
