using System;
using System.Linq;
using System.Net;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.Services.ElectionsService {
	public class ClusterSettingsFactory {
		private const int ManagerPort = 1001;
		private const int StartingPort = 1002;

		private static ClusterVNodeSettings CreateVNode(int nodeNumber) {
			int tcpIntPort = StartingPort + nodeNumber * 2,
				tcpExtPort = tcpIntPort + 1,
				httpIntPort = tcpIntPort + 10,
				httpExtPort = tcpIntPort + 11;

			var vnode = new ClusterVNodeSettings(Guid.NewGuid(), 0,
				GetLoopbackForPort(tcpIntPort), null,
				GetLoopbackForPort(tcpExtPort), null,
				GetLoopbackForPort(httpIntPort), GetLoopbackForPort(httpExtPort),
				new Data.GossipAdvertiseInfo(GetLoopbackForPort(tcpIntPort), null,
					GetLoopbackForPort(tcpExtPort), null,
					GetLoopbackForPort(httpIntPort),
					GetLoopbackForPort(httpExtPort),
					null, null, 0, 0),
				new[] {GetLoopbackForPort(httpIntPort).ToHttpUrl(EndpointExtensions.HTTP_SCHEMA)},
				new[] {GetLoopbackForPort(httpExtPort).ToHttpUrl(EndpointExtensions.HTTP_SCHEMA)},
				false, null, 1, false, "dns", new[] {GetLoopbackForPort(ManagerPort)},
				TFConsts.MinFlushDelayMs, 3, 2, 2, TimeSpan.FromSeconds(2),
				TimeSpan.FromSeconds(2), false, false, null, false, TimeSpan.FromHours(1),
				StatsStorage.StreamAndFile, 0, new InternalAuthenticationProviderFactory(), false, 30, true, true, true,
				TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
				TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(10), true, Opts.MaxMemtableSizeDefault, Opts.HashCollisionReadLimitDefault, false,
				false, false,
				Opts.ConnectionPendingSendBytesThresholdDefault, Opts.ChunkInitialReaderCountDefault);

			return vnode;
		}

		private static IPEndPoint GetLoopbackForPort(int port) {
			return new IPEndPoint(IPAddress.Loopback, port);
		}

		public ClusterSettings GetClusterSettings(int selfIndex, int nodesCount) {
			if (selfIndex < 0 || selfIndex >= nodesCount)
				throw new ArgumentOutOfRangeException("selfIndex", "Index of self should be in range of created nodes");


			var clusterManager = GetLoopbackForPort(ManagerPort);
			var nodes = Enumerable.Range(0, nodesCount).Select(CreateVNode).ToArray();

			var self = nodes[selfIndex];
			var others = nodes.Where((x, i) => i != selfIndex).ToArray();

			var settings = new ClusterSettings("test-dns", clusterManager, self, others, nodes.Length);
			return settings;
		}
	}
}
