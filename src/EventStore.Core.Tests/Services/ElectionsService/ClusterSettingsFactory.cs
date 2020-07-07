using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using EventStore.Common.Utils;
using EventStore.Core.Authentication;
using EventStore.Core.Authentication.InternalAuthentication;
using EventStore.Core.Authorization;
using EventStore.Core.Cluster.Settings;
using EventStore.Core.Services.Monitoring;
using EventStore.Core.Tests.Services.Transport.Tcp;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.Util;

namespace EventStore.Core.Tests.Services.ElectionsService {
	public class ClusterSettingsFactory {
		private const int ManagerPort = 1001;
		private const int StartingPort = 1002;

		private static bool EnableHttps() {
			return !RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
		}

		private static ClusterVNodeSettings CreateVNode(int nodeNumber, bool isReadOnlyReplica) {
			int tcpIntPort = StartingPort + nodeNumber * 2,
				tcpExtPort = tcpIntPort + 1,
				httpPort = tcpIntPort + 11;

			var useHttps = EnableHttps();
			var certificate = useHttps ? ssl_connections.GetServerCertificate() : null;
			var trustedRootCertificate = useHttps ? ssl_connections.GetRootCertificate() : null;

			var vnode = new ClusterVNodeSettings(Guid.NewGuid(), 0,
				() => new ClusterNodeOptions(),
				GetLoopbackForPort(tcpIntPort), null,
				GetLoopbackForPort(tcpExtPort), null,
				GetLoopbackForPort(httpPort),
				new Data.GossipAdvertiseInfo(GetLoopbackForPort(tcpIntPort).ToDnsEndPoint(), null,
					GetLoopbackForPort(tcpExtPort).ToDnsEndPoint(), null,
					GetLoopbackForPort(httpPort).ToDnsEndPoint(),
					null, null, 0),
				false, certificate, new X509Certificate2Collection(trustedRootCertificate),
				Opts.CertificateReservedNodeCommonNameDefault, 1, false, "dns", new[] {GetLoopbackForPort(ManagerPort)},
				TFConsts.MinFlushDelayMs, 3, 2, 2, TimeSpan.FromSeconds(2),
				TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), false, false,TimeSpan.FromHours(1),
				StatsStorage.StreamAndFile, 0,
				new AuthenticationProviderFactory(components =>
					new InternalAuthenticationProviderFactory(components)),
				new AuthorizationProviderFactory(components =>
					new LegacyAuthorizationProviderFactory(components.MainQueue)), false, 30, true, true,
				true, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
				TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(10), 
				TimeSpan.FromSeconds(1800),
				true, Opts.MaxMemtableSizeDefault, Opts.HashCollisionReadLimitDefault, false,
				false, false,
				Opts.ConnectionPendingSendBytesThresholdDefault, Opts.ConnectionQueueSizeThresholdDefault,
				Constants.PTableMaxReaderCountDefault,
				readOnlyReplica: isReadOnlyReplica,
				disableHttps: !useHttps);

			return vnode;
		}

		private static IPEndPoint GetLoopbackForPort(int port) {
			return new IPEndPoint(IPAddress.Loopback, port);
		}

		public ClusterSettings GetClusterSettings(int selfIndex, int nodesCount, bool isSelfReadOnlyReplica) {
			if (selfIndex < 0 || selfIndex >= nodesCount)
				throw new ArgumentOutOfRangeException("selfIndex", "Index of self should be in range of created nodes");


			var clusterManager = GetLoopbackForPort(ManagerPort);
			var nodes = Enumerable.Range(0, nodesCount).Select(x =>
				x == selfIndex ? CreateVNode(x, isSelfReadOnlyReplica) : CreateVNode(x, false)).ToArray();

			var self = nodes[selfIndex];
			var others = nodes.Where((x, i) => i != selfIndex).ToArray();

			var settings = new ClusterSettings("test-dns", clusterManager, self, others, nodes.Length);
			return settings;
		}
	}
}
