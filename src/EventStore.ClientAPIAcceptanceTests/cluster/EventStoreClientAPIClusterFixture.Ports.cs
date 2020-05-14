using System.Net;
using System.Net.Sockets;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIClusterFixture {
		public static readonly int[] HttpPort = new int[ClusterSize];
		public static readonly int[] InternalTcpPort = new int[ClusterSize];
		public static readonly int[] InternalSecureTcpPort = new int[ClusterSize];
		public static readonly int[] ExternalTcpPort = new int[ClusterSize];
		public static readonly int[] ExternalSecureTcpPort = new int[ClusterSize];
		public static readonly int[] UnusedPort = new int[ClusterSize];

		static EventStoreClientAPIClusterFixture() {
			for (var i = 0; i < ClusterSize; i++) {
				ExternalTcpPort[i] = GetFreePort();
				ExternalSecureTcpPort[i] = GetFreePort();
				InternalTcpPort[i] = GetFreePort();
				InternalSecureTcpPort[i] = GetFreePort();
				HttpPort[i] = GetFreePort();
				UnusedPort[i] = GetFreePort();
			}

			static int GetFreePort() {
				using var socket =
					new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
						ExclusiveAddressUse = false
					};
				socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
				return ((IPEndPoint)socket.LocalEndPoint).Port;
			}
		}
	}
}
