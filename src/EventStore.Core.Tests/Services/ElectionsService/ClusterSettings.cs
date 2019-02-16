using System;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Cluster.Settings;

namespace EventStore.Core.Tests.Services.ElectionsService {
	public sealed class ClusterSettings {
		public string ClusterDns { get; private set; }

		public ClusterVNodeSettings Self { get; private set; }
		public ClusterVNodeSettings[] GroupMembers { get; private set; }

		public IPEndPoint ClusterManager { get; private set; }

		public int ClusterNodesCount { get; private set; }

		public ClusterSettings(string clusterDns,
			IPEndPoint clusterManager,
			ClusterVNodeSettings self,
			ClusterVNodeSettings[] groupMembers,
			int expectedNodesCount) {
			if (string.IsNullOrWhiteSpace(clusterDns))
				throw new ArgumentException(string.Format("Wrong cluster DNS name: {0}", clusterDns), clusterDns);
			if (self == null)
				throw new ArgumentNullException("self");
			if (groupMembers == null)
				throw new ArgumentNullException("groupMembers");
			if (clusterManager == null)
				throw new ArgumentNullException("clusterManager");

			ClusterDns = clusterDns;
			Self = self;
			GroupMembers = groupMembers;
			ClusterManager = clusterManager;
			ClusterNodesCount = expectedNodesCount;
		}

		public ClusterSettings(string clusterDns, IPEndPoint clusterManager, ClusterVNodeSettings self,
			int clusterNodesCount)
			: this(clusterDns, clusterManager, self, new ClusterVNodeSettings[0], clusterNodesCount) {
		}

		public ClusterSettings(string clusterDns, IPEndPoint clusterManagerEndPoint, int clusterNodesCount) {
			Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
			Ensure.NotNull(clusterManagerEndPoint, "clusterManagerEndPoint");

			ClusterDns = clusterDns;
			GroupMembers = new ClusterVNodeSettings[0];
			ClusterManager = clusterManagerEndPoint;
			ClusterNodesCount = clusterNodesCount;
		}
	}
}
