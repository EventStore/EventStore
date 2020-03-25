using System;
using System.Net;

namespace EventStore.Client {
	public class EventStoreClientConnectivitySettings {
		/// <summary>
		/// URI of a cluster
		/// </summary>
		public Uri Address { get; set; } = new UriBuilder {
			Scheme = Uri.UriSchemeHttps,
			Port = 2113
		}.Uri;

		/// <summary>
		/// The maximum number of attempts for discovering endpoints.
		/// </summary>
		public int MaxDiscoverAttempts { get; set; }

		/// <summary>
		/// Endpoints for seeding gossip if not using DNS.
		/// </summary>
		public EndPoint[] GossipSeeds {
			get {
				object endpoints = (object)DnsGossipSeeds ?? IpGossipSeeds;
				return endpoints switch
				{
					DnsEndPoint[] dns => Array.ConvertAll<DnsEndPoint, EndPoint>(dns, x => x),
					IPEndPoint[] ip => Array.ConvertAll<IPEndPoint, EndPoint>(ip, x => x),
					_ => Array.Empty<EndPoint>()
				};
			}
		}

		/// <summary>
		/// Endpoints for seeding gossip when using DNS.
		/// </summary>
		public DnsEndPoint[] DnsGossipSeeds { get; set; }

		/// <summary>
		/// Endpoints for seeding gossip if using IP adresses.
		/// </summary>
		public IPEndPoint[] IpGossipSeeds { get; set; }

		/// <summary>
		/// Timeout for cluster gossip.
		/// </summary>
		public TimeSpan GossipTimeout { get; set; }

		/// <summary>
		/// Interval between discovering each node.
		/// </summary>
		public TimeSpan DiscoveryInterval { get; set; }

		/// <summary>
		/// Prefer a randomly selected node. 
		/// </summary>
		public NodePreference NodePreference { get; set; }

		public static EventStoreClientConnectivitySettings Default => new EventStoreClientConnectivitySettings {
			MaxDiscoverAttempts = 10,
			GossipTimeout = TimeSpan.FromSeconds(5),
			DiscoveryInterval = TimeSpan.FromMilliseconds(100),
			NodePreference = NodePreference.Leader,
		};
	}
}
