using System;
using EventStore.ClientAPI.Common.Utils;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Contains settings relating to a connection to a cluster. 
	/// </summary>
	public sealed class ClusterSettings {
		/// <summary>
		/// Creates a new set of <see cref="ClusterSettings"/>
		/// </summary>
		/// <returns>A <see cref="ClusterSettingsBuilder"/> that can be used to build up a <see cref="ClusterSettings"/></returns>
		public static ClusterSettingsBuilder Create() {
			return new ClusterSettingsBuilder();
		}

		/// <summary>
		/// The DNS name to use for discovering endpoints.
		/// </summary>
		public readonly string ClusterDns;

		/// <summary>
		/// The maximum number of attempts for discovering endpoints.
		/// </summary>
		public readonly int MaxDiscoverAttempts;

		/// <summary>
		/// The well-known endpoint on which cluster managers are running.
		/// </summary>
		public readonly int ExternalGossipPort;

		/// <summary>
		/// Endpoints for seeding gossip if not using DNS.
		/// </summary>
		public readonly GossipSeed[] GossipSeeds;

		/// <summary>
		/// Timeout for cluster gossip.
		/// </summary>
		public TimeSpan GossipTimeout;

		/// <summary>
		/// Prefer a randomly selected node. 
		/// </summary>
		public NodePreference NodePreference;

		/// <summary>
		/// Used if connecting with gossip seeds.
		/// </summary>
		/// <param name="gossipSeeds">Endpoints for seeding gossip</param>.
		/// <param name="maxDiscoverAttempts">Maximum number of attempts to discover the cluster</param>.
		/// <param name="gossipTimeout">Timeout for cluster gossip</param>.
		/// <param name="nodePreference">Whether to prefer slave, random, or master node selection</param>.
		internal ClusterSettings(GossipSeed[] gossipSeeds, int maxDiscoverAttempts, TimeSpan gossipTimeout,
			NodePreference nodePreference) {
			ClusterDns = "";
			MaxDiscoverAttempts = maxDiscoverAttempts;
			ExternalGossipPort = 0;
			GossipTimeout = gossipTimeout;
			GossipSeeds = gossipSeeds;
			NodePreference = nodePreference;
		}

		/// <summary>
		/// Used if discovering via DNS.
		/// </summary>
		/// <param name="clusterDns">The DNS name to use for discovering endpoints</param>.
		/// <param name="maxDiscoverAttempts">The maximum number of attempts for discovering endpoints</param>.
		/// <param name="externalGossipPort">The well-known endpoint on which cluster managers are running</param>.
		/// <param name="gossipTimeout">Timeout for cluster gossip</param>.
		/// <param name="nodePreference">Whether to prefer slave, random, or master node selection</param>.
		internal ClusterSettings(string clusterDns, int maxDiscoverAttempts, int externalGossipPort,
			TimeSpan gossipTimeout, NodePreference nodePreference) {
			Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
			if (maxDiscoverAttempts < -1)
				throw new ArgumentOutOfRangeException("maxDiscoverAttempts",
					string.Format("maxDiscoverAttempts value is out of range: {0}. Allowed range: [-1, infinity].",
						maxDiscoverAttempts));
			Ensure.Positive(externalGossipPort, "externalGossipPort");

			ClusterDns = clusterDns;
			MaxDiscoverAttempts = maxDiscoverAttempts;
			ExternalGossipPort = externalGossipPort;
			GossipTimeout = gossipTimeout;
			GossipSeeds = new GossipSeed[0];
			NodePreference = nodePreference;
		}
	}
}
