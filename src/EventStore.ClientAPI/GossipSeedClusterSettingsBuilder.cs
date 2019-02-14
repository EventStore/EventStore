using System;
using System.Linq;
using System.Net;

namespace EventStore.ClientAPI {
	/// <summary>
	/// Fluent builder used to configure <see cref="ClusterSettings" /> for connecting to a cluster
	/// using gossip seeds.
	/// </summary>
	public class GossipSeedClusterSettingsBuilder {
		private GossipSeed[] _gossipSeeds;
		private TimeSpan _gossipTimeout = TimeSpan.FromSeconds(1);
		private int _maxDiscoverAttempts = Consts.DefaultMaxClusterDiscoverAttempts;
		private NodePreference _nodePreference = NodePreference.Master;

		/// <summary>
		/// Sets gossip seed endpoints for the client.
		/// TODO: This was a note.
		/// This should be the external HTTP endpoint of the server, as it is required
		/// for the client to exchange gossip with the server. The standard port is 2113.
		/// 
		/// If the server requires a specific Host header to be sent as part of the gossip
		/// request, use the overload of this method taking <see cref="GossipSeed" /> instead.
		/// </summary>
		/// <param name="gossipSeeds"><see cref="IPEndPoint" />s representing the endpoints of nodes from which to seed gossip.</param>
		/// <returns>A <see cref="ClusterSettingsBuilder"/> for further configuration.</returns>
		/// <exception cref="ArgumentException">If no gossip seeds are specified.</exception>
		public GossipSeedClusterSettingsBuilder SetGossipSeedEndPoints(params IPEndPoint[] gossipSeeds) {
			if (gossipSeeds == null || gossipSeeds.Length == 0)
				throw new ArgumentException("Empty FakeDnsEntries collection.");

			_gossipSeeds = gossipSeeds.Select(x => new GossipSeed(x)).ToArray();

			return this;
		}

		/// <summary>
		/// Sets gossip seed endpoints for the client.
		/// </summary>
		/// <param name="gossipSeeds"><see cref="GossipSeed"/>s representing the endpoints of nodes from which to seed gossip.</param>
		/// <returns>A <see cref="ClusterSettingsBuilder"/> for further configuration.</returns>
		/// <exception cref="ArgumentException">If no gossip seeds are specified.</exception>
		public GossipSeedClusterSettingsBuilder SetGossipSeedEndPoints(params GossipSeed[] gossipSeeds) {
			if (gossipSeeds == null || gossipSeeds.Length == 0)
				throw new ArgumentException("Empty FakeDnsEntries collection.");
			_gossipSeeds = gossipSeeds;
			return this;
		}

		/// <summary>
		/// Allows infinite nodes discovery attempts.
		/// </summary>
		/// <returns></returns>
		public GossipSeedClusterSettingsBuilder KeepDiscovering() {
			_maxDiscoverAttempts = Int32.MaxValue;
			return this;
		}

		/// <summary>
		/// Sets the maximum number of attempts for discovery.
		/// </summary>
		/// <param name="maxDiscoverAttempts">The maximum number of attempts for DNS discovery.</param>
		/// <returns>A <see cref="GossipSeedClusterSettingsBuilder"/> for further configuration.</returns>
		/// <exception cref="ArgumentOutOfRangeException">If <paramref name="maxDiscoverAttempts" /> is less than or equal to 0.</exception>
		public GossipSeedClusterSettingsBuilder SetMaxDiscoverAttempts(int maxDiscoverAttempts) {
			if (maxDiscoverAttempts <= 0)
				throw new ArgumentOutOfRangeException("maxDiscoverAttempts",
					string.Format("maxDiscoverAttempts value is out of range: {0}. Allowed range: [1, infinity].",
						maxDiscoverAttempts));
			_maxDiscoverAttempts = maxDiscoverAttempts;
			return this;
		}

		/// <summary>
		/// Sets the period after which gossip times out if none is received.
		/// </summary>
		/// <param name="timeout">The period after which gossip times out if none is received.</param>
		/// <returns>A <see cref="GossipSeedClusterSettingsBuilder"/> for further configuration.</returns>
		public GossipSeedClusterSettingsBuilder SetGossipTimeout(TimeSpan timeout) {
			_gossipTimeout = timeout;
			return this;
		}

		/// <summary>
		/// Whether to randomly choose a node that's alive from the known nodes. 
		/// </summary>
		/// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
		public GossipSeedClusterSettingsBuilder PreferRandomNode() {
			_nodePreference = NodePreference.Random;
			return this;
		}

		/// <summary>
		/// Whether to prioritize choosing a slave node that's alive from the known nodes. 
		/// </summary>
		/// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
		public GossipSeedClusterSettingsBuilder PreferSlaveNode() {
			_nodePreference = NodePreference.Slave;
			return this;
		}

		/// <summary>
		/// Builds a <see cref="ClusterSettings"/> object from a <see cref="GossipSeedClusterSettingsBuilder"/>.
		/// </summary>
		/// <param name="builder"><see cref="GossipSeedClusterSettingsBuilder"/> from which to build a <see cref="ClusterSettings"/></param>
		/// <returns></returns>
		public static implicit operator ClusterSettings(GossipSeedClusterSettingsBuilder builder) {
			return builder.Build();
		}

		/// <summary>
		/// Builds a <see cref="ClusterSettings"/> object from a <see cref="GossipSeedClusterSettingsBuilder"/>.
		/// </summary>
		public ClusterSettings Build() {
			return new ClusterSettings(this._gossipSeeds, this._maxDiscoverAttempts, this._gossipTimeout,
				this._nodePreference);
		}
	}
}
