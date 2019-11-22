using System.Net;
using System.Net.Sockets;

namespace EventStore.ClientAPI.Tests {
	partial class EventStoreClientAPIFixture {
		public static readonly int ExternalPort;
		public static readonly int ExternalSecurePort;
		public static readonly int UnusedPort;

		static EventStoreClientAPIFixture() {
			ExternalPort = GetFreePort();
			ExternalSecurePort = GetFreePort();
			UnusedPort = GetFreePort();

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
